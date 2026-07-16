using System.Globalization;
using System.Reflection;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Processors
{
    /// <summary>
    /// Combines a column's compiled setter and a value parser into a single
    /// <see cref="ColumnParser{TModel}"/> closure. The column span is sliced by the parser and passed
    /// in, so these closures only parse and assign. Resolution order: a <c>FixedColumnAttribute.Converter</c>
    /// when set, then <c>string</c> (special-cased: never fails, uses the <see cref="StringPool"/>), then
    /// <c>double</c>/<c>float</c> (csFastFloat), then the <see cref="ISpanParsable{TSelf}"/> fallback.
    /// </summary>
    internal static class ColumnParserFactory
    {
        private static readonly MethodInfo BuildParsableMethod =
            typeof(ColumnParserFactory).GetMethod(nameof(BuildParsable), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo BuildConverterMethod =
            typeof(ColumnParserFactory).GetMethod(nameof(BuildConverter), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo BuildDoubleMethod =
            typeof(ColumnParserFactory).GetMethod(nameof(BuildDouble), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo BuildFloatMethod =
            typeof(ColumnParserFactory).GetMethod(nameof(BuildFloat), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo BuildNullableMethod =
            typeof(ColumnParserFactory).GetMethod(nameof(BuildNullable), BindingFlags.NonPublic | BindingFlags.Static)!;

        /// <summary>
        /// Creates a <see cref="ColumnParser{TModel}"/> for the given member type and setter. The setter is a compiled expression assigning the parsed value to the member.
        /// When <paramref name="valueType"/> is a <see cref="Nullable{T}"/>, a blank (trimmed-empty) column assigns
        /// <see langword="null"/> without invoking the underlying parser; otherwise resolution for the underlying <c>T</c>
        /// proceeds exactly as for a non-nullable column. When <paramref name="converterType"/> is set (from
        /// <c>FixedColumnAttribute.Converter</c>), it takes priority over the <see cref="ISpanParsable{TSelf}"/> fallback.
        /// </summary>
        /// <remarks><paramref name="setter"/> must be a <c>RefAction&lt;TModel, valueType&gt;</c> assigning the parsed value to the member.</remarks>
        public static ColumnParser<TModel> Create<TModel>(Type valueType, Delegate setter, Type? converterType, string memberName, char trimChar = ' ', Attributes.TrimMode trimMode = Attributes.TrimMode.Trailing, string? format = null)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            var underlyingType = Nullable.GetUnderlyingType(valueType);
            if (underlyingType is not null)
            {
                return (ColumnParser<TModel>)BuildNullableMethod.MakeGenericMethod(typeof(TModel), underlyingType)
                                                                .Invoke(null, [setter, converterType, memberName, trimChar, trimMode, format])!;
            }

            return ColumnParserFactoryShared.Create<TModel, ColumnParser<TModel>>(
                valueType, setter, converterType, memberName, trimChar, trimMode, format, typeof(IFixedWidthConverter<>),
                BuildParsableMethod, BuildConverterMethod, BuildDoubleMethod, BuildFloatMethod, BuildString, BuildExact<TModel>);
        }

        // A nullable column (T?): a blank (trimmed-empty) column assigns null without invoking the
        // underlying parser. Otherwise, the underlying T resolves exactly as a non-nullable column
        // would (converter → double/float → ISpanParsable fallback), via an adapter setter that boxes
        // the result into the T? setter — no duplicated parse logic per type.
        private static ColumnParser<TModel> BuildNullable<TModel, TUnderlying>(Delegate setter, Type? converterType, string memberName, char trimChar, Attributes.TrimMode trimMode, string? format)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
            where TUnderlying : struct
        {
            var nullableSetter = (RefAction<TModel, TUnderlying?>)setter;
            RefAction<TModel, TUnderlying> adapted = (ref TModel model, TUnderlying value) => nullableSetter(ref model, value);

            var inner = ColumnParserFactoryShared.Create<TModel, ColumnParser<TModel>>(
                typeof(TUnderlying), adapted, converterType, memberName, trimChar, trimMode, format, typeof(IFixedWidthConverter<>),
                BuildParsableMethod, BuildConverterMethod, BuildDoubleMethod, BuildFloatMethod, BuildString, BuildExact<TModel>);

            return (column, formatProvider, stringPool, ref model) =>
            {
                if (column.Trim(trimChar).Trim(' ').IsEmpty)
                {
                    nullableSetter(ref model, null);
                    return true;
                }
                return inner(column, formatProvider, stringPool, ref model);
            };
        }

        // Fallback for types with no dedicated builder: call the static-abstract ISpanParsable.TryParse
        // directly inside the column closure. This is a constrained call the JIT devirtualizes to the
        // concrete TValue.TryParse (and can inline), saving one indirect call per column and no
        // value-parser delegate allocated.
        private static ColumnParser<TModel> BuildParsable<TModel, TValue>(RefAction<TModel, TValue> setter, char trimChar, Attributes.TrimMode trimMode)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
            where TValue : ISpanParsable<TValue>
        {
            return (column, formatProvider, _, ref model) =>
            {
                // Null means invariant (matches FixedWidthRuntime.TryParse<TValue> and the double/float
                // columns) rather than the BCL's own CurrentCulture default.
                if (!TValue.TryParse(FixedWidthRuntime.TrimColumn(column, trimChar, trimMode), formatProvider ?? CultureInfo.InvariantCulture, out var value))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static ColumnParser<TModel> BuildDouble<TModel>(RefAction<TModel, double> setter, char trimChar, Attributes.TrimMode trimMode)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            return (column, formatProvider, _, ref model) =>
            {
                if (!CultureHelpers.TryParseDouble(column, formatProvider, out double value, trimChar, trimMode))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static ColumnParser<TModel> BuildFloat<TModel>(RefAction<TModel, float> setter, char trimChar, Attributes.TrimMode trimMode)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            return (column, formatProvider, _, ref model) =>
            {
                if (!CultureHelpers.TryParseFloat(column, formatProvider, out float value, trimChar, trimMode))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        // Attribute-driven custom converter (FixedColumnAttribute.Converter): the instance is created
        // once (by the caller) and reused for every row, so it must be stateless.
        private static ColumnParser<TModel> BuildConverter<TModel, TValue, TConverter>(
            RefAction<TModel, TValue> setter, TConverter converter, char trimChar, Attributes.TrimMode trimMode)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
            where TConverter : IFixedWidthConverter<TValue>
        {
            return (column, formatProvider, _, ref model) =>
            {
                if (!converter.TryParse(FixedWidthRuntime.TrimColumn(column, trimChar, trimMode), formatProvider, out var value))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static ColumnParser<TModel> BuildString<TModel>(RefAction<TModel, string> setter, object trimChar, Attributes.TrimMode trimMode)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            char trim = (char)trimChar;
            return (column, _, stringPool, ref model) =>
            {
                var slice = FixedWidthRuntime.TrimColumn(column, trim, trimMode);
                setter(ref model, stringPool is null ? slice.ToString() : stringPool.GetOrAdd(slice));
                return true;
            };
        }

        private static ColumnParser<TModel> BuildExact<TModel>(Type valueType, Delegate setter, string format, object trimChar, Attributes.TrimMode trimMode)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            char trim = (char)trimChar;
            if (valueType == typeof(DateTime))
            {
                return BuildExact((RefAction<TModel, DateTime>)setter, format, trim, trimMode, DateTime.TryParseExact);
            }
            if (valueType == typeof(DateOnly))
            {
                return BuildExact((RefAction<TModel, DateOnly>)setter, format, trim, trimMode, DateOnly.TryParseExact);
            }
            if (valueType == typeof(TimeOnly))
            {
                return BuildExact((RefAction<TModel, TimeOnly>)setter, format, trim, trimMode, TimeOnly.TryParseExact);
            }
            if (valueType == typeof(DateTimeOffset))
            {
                return BuildExact((RefAction<TModel, DateTimeOffset>)setter, format, trim, trimMode, DateTimeOffset.TryParseExact);
            }
            throw new InvalidOperationException($"Unsupported TryParseExact type {valueType}");
        }

        private delegate bool ExactParser<TValue>(ReadOnlySpan<char> value, ReadOnlySpan<char> format, IFormatProvider? formatProvider, DateTimeStyles styles, out TValue result);

        private static ColumnParser<TModel> BuildExact<TModel, TValue>(RefAction<TModel, TValue> setter, string format, char trim, Attributes.TrimMode trimMode, ExactParser<TValue> parser)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            return (column, fp, _, ref model) =>
            {
                var trimmed = FixedWidthRuntime.TrimColumn(column, trim, trimMode);
                if (!parser(trimmed, format, fp ?? CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }
    }
}
