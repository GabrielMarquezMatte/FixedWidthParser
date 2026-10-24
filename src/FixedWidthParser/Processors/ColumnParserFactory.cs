using System.Reflection;
using FixedWidthParser.Attributes;

namespace FixedWidthParser.Processors
{
    /// <summary>
    /// Combines a column's compiled setter and a value parser into a single
    /// <see cref="ColumnParser{TModel}"/> closure that calls the same <see cref="FixedWidthRuntime"/>
    /// helpers as generated code. Resolution order: a <c>FixedColumnAttribute.Converter</c>, a date or
    /// time with a <c>Format</c>, <c>string</c>, <c>double</c>/<c>float</c>, then
    /// <see cref="IUtf8SpanParsable{TSelf}"/>, then <see cref="ISpanParsable{TSelf}"/> over the decoded text.
    /// </summary>
    internal static class ColumnParserFactory
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;

        public static ColumnParser<TModel> Create<TModel>(Type valueType, Delegate setter, Type? converterType, string memberName, char trimChar, TrimMode trimMode, string? format)
            where TModel : allows ref struct
        {
            byte trim = FixedWidthRuntime.ToAsciiByte(trimChar, memberName, nameof(FixedColumnAttribute.TrimChar));
            var underlyingType = Nullable.GetUnderlyingType(valueType);
            if (underlyingType is not null)
            {
                return (ColumnParser<TModel>)Method(nameof(BuildNullable), typeof(TModel), underlyingType)
                    .Invoke(null, [setter, converterType, memberName, trim, trimMode, format])!;
            }
            return CreateValue<TModel>(valueType, setter, converterType, memberName, trim, trimMode, format);
        }

        private static ColumnParser<TModel> CreateValue<TModel>(Type valueType, Delegate setter, Type? converterType, string memberName, byte trim, TrimMode trimMode, string? format)
            where TModel : allows ref struct
        {
            if (converterType is not null)
            {
                var requiredInterface = typeof(IFixedWidthConverter<>).MakeGenericType(valueType);
                if (!requiredInterface.IsAssignableFrom(converterType))
                {
                    throw new InvalidOperationException(
                        $"Converter '{converterType}' for column '{memberName}' must implement '{requiredInterface}'.");
                }
                var converter = Activator.CreateInstance(converterType)
                    ?? throw new InvalidOperationException($"Could not instantiate converter '{converterType}' for column '{memberName}'.");
                return (ColumnParser<TModel>)Method(nameof(BuildConverter), typeof(TModel), valueType, converterType)
                    .Invoke(null, [setter, converter, trim, trimMode])!;
            }

            if (format is not null)
            {
                if (valueType == typeof(DateTime))
                {
                    return BuildDateTime((ColumnSetter<TModel, DateTime>)setter, format, trim, trimMode);
                }
                if (valueType == typeof(DateOnly))
                {
                    return BuildDateOnly((ColumnSetter<TModel, DateOnly>)setter, format, trim, trimMode);
                }
                if (valueType == typeof(TimeOnly))
                {
                    return BuildTimeOnly((ColumnSetter<TModel, TimeOnly>)setter, format, trim, trimMode);
                }
                if (valueType == typeof(DateTimeOffset))
                {
                    return BuildDateTimeOffset((ColumnSetter<TModel, DateTimeOffset>)setter, format, trim, trimMode);
                }
            }

            if (valueType == typeof(string))
            {
                return BuildString((ColumnSetter<TModel, string>)setter, trim, trimMode);
            }
            if (valueType == typeof(double))
            {
                return BuildDouble((ColumnSetter<TModel, double>)setter, trim, trimMode);
            }
            if (valueType == typeof(float))
            {
                return BuildFloat((ColumnSetter<TModel, float>)setter, trim, trimMode);
            }
            if (Implements(valueType, typeof(IUtf8SpanParsable<>)))
            {
                return (ColumnParser<TModel>)Method(nameof(BuildUtf8Parsable), typeof(TModel), valueType).Invoke(null, [setter, trim, trimMode])!;
            }
            if (Implements(valueType, typeof(ISpanParsable<>)))
            {
                return (ColumnParser<TModel>)Method(nameof(BuildDecodedParsable), typeof(TModel), valueType).Invoke(null, [setter, trim, trimMode])!;
            }

            throw new InvalidOperationException(
                $"Column '{memberName}' has type '{valueType}', which is not string, double, float, IUtf8SpanParsable<T> or ISpanParsable<T>; set a Converter for it.");
        }

        private static bool Implements(Type valueType, Type openInterface)
        {
            // Not MakeGenericType: it throws for a type that breaks the interface's self-constraint.
            return Array.Exists(valueType.GetInterfaces(), i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == openInterface && i.GenericTypeArguments[0] == valueType);
        }

        private static MethodInfo Method(string name, params Type[] typeArguments)
        {
            return typeof(ColumnParserFactory).GetMethod(name, Private)!.MakeGenericMethod(typeArguments);
        }

        // A nullable column (T?): a blank column assigns null without invoking the underlying parser.
        private static ColumnParser<TModel> BuildNullable<TModel, TUnderlying>(Delegate setter, Type? converterType, string memberName, byte trim, TrimMode trimMode, string? format)
            where TModel : allows ref struct
            where TUnderlying : struct
        {
            var nullableSetter = (ColumnSetter<TModel, TUnderlying?>)setter;
            ColumnSetter<TModel, TUnderlying> adapted = (ref TModel model, TUnderlying value) => nullableSetter(ref model, value);
            var inner = CreateValue<TModel>(typeof(TUnderlying), adapted, converterType, memberName, trim, trimMode, format);

            return (column, in context, ref model) =>
            {
                if (FixedWidthRuntime.IsBlank(column, trim))
                {
                    nullableSetter(ref model, null);
                    return true;
                }
                return inner(column, in context, ref model);
            };
        }

        private static ColumnParser<TModel> BuildUtf8Parsable<TModel, TValue>(ColumnSetter<TModel, TValue> setter, byte trim, TrimMode trimMode)
            where TModel : allows ref struct
            where TValue : IUtf8SpanParsable<TValue>
        {
            return (column, in context, ref model) =>
            {
                if (!FixedWidthRuntime.TryParse<TValue>(column, in context, out var value, trim, trimMode))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static ColumnParser<TModel> BuildDecodedParsable<TModel, TValue>(ColumnSetter<TModel, TValue> setter, byte trim, TrimMode trimMode)
            where TModel : allows ref struct
            where TValue : ISpanParsable<TValue>
        {
            return (column, in context, ref model) =>
            {
                if (!FixedWidthRuntime.TryParseDecoded<TValue>(column, in context, out var value, trim, trimMode))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static ColumnParser<TModel> BuildConverter<TModel, TValue, TConverter>(ColumnSetter<TModel, TValue> setter, TConverter converter, byte trim, TrimMode trimMode)
            where TModel : allows ref struct
            where TConverter : IFixedWidthConverter<TValue>
        {
            return (column, in context, ref model) =>
            {
                if (!FixedWidthRuntime.TryConvert<TValue, TConverter>(column, in context, converter, out var value, trim, trimMode))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static ColumnParser<TModel> BuildString<TModel>(ColumnSetter<TModel, string> setter, byte trim, TrimMode trimMode)
            where TModel : allows ref struct
        {
            return (column, in context, ref model) =>
            {
                setter(ref model, FixedWidthRuntime.String(column, in context, trim, trimMode));
                return true;
            };
        }

        private static ColumnParser<TModel> BuildDouble<TModel>(ColumnSetter<TModel, double> setter, byte trim, TrimMode trimMode)
            where TModel : allows ref struct
        {
            return (column, in context, ref model) =>
            {
                if (!FixedWidthRuntime.TryDouble(column, in context, out double value, trim, trimMode))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static ColumnParser<TModel> BuildFloat<TModel>(ColumnSetter<TModel, float> setter, byte trim, TrimMode trimMode)
            where TModel : allows ref struct
        {
            return (column, in context, ref model) =>
            {
                if (!FixedWidthRuntime.TryFloat(column, in context, out float value, trim, trimMode))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static ColumnParser<TModel> BuildDateTime<TModel>(ColumnSetter<TModel, DateTime> setter, string format, byte trim, TrimMode trimMode)
            where TModel : allows ref struct
        {
            return (column, in context, ref model) =>
            {
                if (!FixedWidthRuntime.TryDateTimeExact(column, format, in context, out var value, trim, trimMode))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static ColumnParser<TModel> BuildDateOnly<TModel>(ColumnSetter<TModel, DateOnly> setter, string format, byte trim, TrimMode trimMode)
            where TModel : allows ref struct
        {
            return (column, in context, ref model) =>
            {
                if (!FixedWidthRuntime.TryDateOnlyExact(column, format, in context, out var value, trim, trimMode))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static ColumnParser<TModel> BuildTimeOnly<TModel>(ColumnSetter<TModel, TimeOnly> setter, string format, byte trim, TrimMode trimMode)
            where TModel : allows ref struct
        {
            return (column, in context, ref model) =>
            {
                if (!FixedWidthRuntime.TryTimeOnlyExact(column, format, in context, out var value, trim, trimMode))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static ColumnParser<TModel> BuildDateTimeOffset<TModel>(ColumnSetter<TModel, DateTimeOffset> setter, string format, byte trim, TrimMode trimMode)
            where TModel : allows ref struct
        {
            return (column, in context, ref model) =>
            {
                if (!FixedWidthRuntime.TryDateTimeOffsetExact(column, format, in context, out var value, trim, trimMode))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }
    }
}
