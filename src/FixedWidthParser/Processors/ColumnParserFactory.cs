using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FixedWidthParser.Infrastructure;

namespace FixedWidthParser.Processors
{
    /// <summary>
    /// Combines a column's setter and a value parser into a single <see cref="ColumnParser{TModel}"/>
    /// closure that calls the same <see cref="FixedWidthRuntime"/> helpers as generated code. Resolution
    /// order: a converter, a date or time with a <c>Format</c>, <c>string</c>, <c>double</c>/<c>float</c>,
    /// an enum, then <see cref="IUtf8SpanParsable{TSelf}"/>, then <see cref="ISpanParsable{TSelf}"/> over the
    /// decoded text. Attribute layouts reach it by reflection (<see cref="Create{TModel}"/>), fluent layouts through
    /// the typed <c>For*</c> entry points.
    /// </summary>
    internal static class ColumnParserFactory
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;

        public static ColumnParser<TModel> Create<TModel>(Type valueType, Delegate setter, Type? converterType, string memberName, char trimChar, TrimMode trimMode, string? format)
            where TModel : allows ref struct
        {
            FixedWidthRuntime.ToAsciiByte(trimChar, memberName, nameof(FixedColumnAttribute.TrimChar));
            var options = new ColumnParseOptions(trimChar, trimMode, format);
            var underlyingType = Nullable.GetUnderlyingType(valueType);
            if (underlyingType is not null)
            {
                return (ColumnParser<TModel>)Method(nameof(BuildNullable), typeof(TModel), underlyingType)
                    .Invoke(null, [setter, converterType, memberName, options])!;
            }
            return CreateValue<TModel>(valueType, setter, converterType, memberName, options);
        }

        private static ColumnParser<TModel> CreateValue<TModel>(Type valueType, Delegate setter, Type? converterType, string memberName, ColumnParseOptions options)
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
                return (ColumnParser<TModel>)Method(nameof(ForConverter), typeof(TModel), valueType, converterType)
                    .Invoke(null, [setter, converter, options])!;
            }

            var special = TryBuildSpecial<TModel>(valueType, setter, options);
            if (special is not null)
            {
                return special;
            }
            if (valueType == typeof(string))
            {
                return ForString((ColumnSetter<TModel, string>)setter, options);
            }
            if (valueType.IsEnum)
            {
                return (ColumnParser<TModel>)Method(nameof(ForEnum), typeof(TModel), valueType).Invoke(null, [setter, options])!;
            }
            if (Implements(valueType, typeof(IUtf8SpanParsable<>)))
            {
                return (ColumnParser<TModel>)Method(nameof(ForUtf8Parsable), typeof(TModel), valueType).Invoke(null, [setter, options])!;
            }
            if (Implements(valueType, typeof(ISpanParsable<>)))
            {
                return (ColumnParser<TModel>)Method(nameof(ForDecodedParsable), typeof(TModel), valueType).Invoke(null, [setter, options])!;
            }

            throw new InvalidOperationException(
                $"Column '{memberName}' has type '{valueType}', which is not string, double, float, an enum, IUtf8SpanParsable<T> or ISpanParsable<T>; set a Converter for it.");
        }

        internal static ColumnParser<TModel> ForEnum<TModel, TEnum>(ColumnSetter<TModel, TEnum> setter, ColumnParseOptions options)
            where TModel : allows ref struct
            where TEnum : struct, Enum
        {
            return Bind(setter, options, FixedWidthRuntime.TryEnum);
        }

        internal static ColumnParser<TModel> ForString<TModel>(ColumnSetter<TModel, string> setter, ColumnParseOptions options)
            where TModel : allows ref struct
        {
            return (column, in context, ref model) =>
            {
                setter(ref model, FixedWidthRuntime.String(column, in context, in options));
                return true;
            };
        }

        internal static ColumnParser<TModel> ForUtf8Parsable<TModel, TValue>(ColumnSetter<TModel, TValue> setter, ColumnParseOptions options)
            where TModel : allows ref struct
            where TValue : IUtf8SpanParsable<TValue>
        {
            return TryBuildSpecial<TModel>(typeof(TValue), setter, options) ?? Bind(setter, options, FixedWidthRuntime.TryParse);
        }

        internal static ColumnParser<TModel> ForDecodedParsable<TModel, TValue>(ColumnSetter<TModel, TValue> setter, ColumnParseOptions options)
            where TModel : allows ref struct
            where TValue : ISpanParsable<TValue>
        {
            return TryBuildSpecial<TModel>(typeof(TValue), setter, options) ?? Bind(setter, options, FixedWidthRuntime.TryParseDecoded);
        }

        // TConverter is the concrete type on the attribute path, so its calls devirtualize there.
        internal static ColumnParser<TModel> ForConverter<TModel, TValue, TConverter>(ColumnSetter<TModel, TValue> setter, TConverter converter, ColumnParseOptions options)
            where TModel : allows ref struct
            where TConverter : IFixedWidthConverter<TValue>
        {
            return Bind(setter, options, (ReadOnlySpan<byte> column, in FixedWidthContext context, in ColumnParseOptions o, [MaybeNullWhen(false)] out TValue value) =>
                FixedWidthRuntime.TryConvert(column, in context, converter, in o, out value));
        }

        internal static ColumnParser<TModel> ForNullable<TModel, TValue>(
            ColumnSetter<TModel, TValue?> setter, ColumnParseOptions options, Func<ColumnSetter<TModel, TValue>, ColumnParser<TModel>> buildInner)
            where TModel : allows ref struct
            where TValue : struct
        {
            ColumnSetter<TModel, TValue> adapted = (ref TModel model, TValue value) => setter(ref model, value);
            return WrapNullable(setter, buildInner(adapted), options);
        }

        // The types the attribute path special-cases ahead of the generic parsers, in its order: a date or
        // time with a Format, then double and float.
        private static ColumnParser<TModel>? TryBuildSpecial<TModel>(Type valueType, Delegate setter, ColumnParseOptions options)
            where TModel : allows ref struct
        {
            if (options.Format is not null)
            {
                var date = TryBuildDate<TModel>(valueType, setter, options);
                if (date is not null)
                {
                    return date;
                }
            }
            if (valueType == typeof(double))
            {
                return Bind((ColumnSetter<TModel, double>)setter, options, FixedWidthRuntime.TryDouble);
            }
            if (valueType == typeof(float))
            {
                return Bind((ColumnSetter<TModel, float>)setter, options, FixedWidthRuntime.TryFloat);
            }
            return null;
        }

        private static ColumnParser<TModel>? TryBuildDate<TModel>(Type valueType, Delegate setter, ColumnParseOptions options)
            where TModel : allows ref struct
        {
            if (valueType == typeof(DateTime))
            {
                return Bind((ColumnSetter<TModel, DateTime>)setter, options, FixedWidthRuntime.TryDateTimeExact);
            }
            if (valueType == typeof(DateOnly))
            {
                return Bind((ColumnSetter<TModel, DateOnly>)setter, options, FixedWidthRuntime.TryDateOnlyExact);
            }
            if (valueType == typeof(TimeOnly))
            {
                return Bind((ColumnSetter<TModel, TimeOnly>)setter, options, FixedWidthRuntime.TryTimeOnlyExact);
            }
            if (valueType == typeof(DateTimeOffset))
            {
                return Bind((ColumnSetter<TModel, DateTimeOffset>)setter, options, FixedWidthRuntime.TryDateTimeOffsetExact);
            }
            return null;
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
        private static ColumnParser<TModel> BuildNullable<TModel, TUnderlying>(Delegate setter, Type? converterType, string memberName, ColumnParseOptions options)
            where TModel : allows ref struct
            where TUnderlying : struct
        {
            return ForNullable<TModel, TUnderlying>((ColumnSetter<TModel, TUnderlying?>)setter, options,
                adapted => CreateValue<TModel>(typeof(TUnderlying), adapted, converterType, memberName, options));
        }

        private static ColumnParser<TModel> WrapNullable<TModel, TUnderlying>(ColumnSetter<TModel, TUnderlying?> nullableSetter, ColumnParser<TModel> inner, ColumnParseOptions options)
            where TModel : allows ref struct
            where TUnderlying : struct
        {
            return (column, in context, ref model) =>
            {
                if (FixedWidthRuntime.IsBlank(column, in options))
                {
                    nullableSetter(ref model, null);
                    return true;
                }
                return inner(column, in context, ref model);
            };
        }

        private static ColumnParser<TModel> Bind<TModel, TValue>(ColumnSetter<TModel, TValue> setter, ColumnParseOptions options, ColumnValueParser<TValue> parse)
            where TModel : allows ref struct
        {
            return (column, in context, ref model) =>
            {
                if (!parse(column, in context, in options, out var value))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }
    }
}
