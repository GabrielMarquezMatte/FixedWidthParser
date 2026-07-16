using System.Globalization;
using System.Reflection;
using System.Text;

namespace FixedWidthParser.Processors
{
    /// <summary>
    /// UTF-8 counterpart of <see cref="ColumnParserFactory"/>: combines a column's compiled setter and
    /// a UTF-8 value parser into a single <see cref="Utf8ColumnParser{TModel}"/> closure. The column
    /// span is sliced (in bytes) by the parser and passed in, so these closures only parse and assign.
    /// Resolution order: a <c>FixedColumnAttribute.Converter</c> when set, then <c>string</c> (special-cased:
    /// never fails, UTF-8 decoded, interned through the supplied <see cref="CommunityToolkit.HighPerformance.Buffers.StringPool"/>
    /// when one is provided), then <c>double</c>/<c>float</c> (csFastFloat), then the
    /// <see cref="IUtf8SpanParsable{TSelf}"/> fallback.
    /// </summary>
    internal static class Utf8ColumnParserFactory
    {
        private static readonly MethodInfo BuildParsableMethod =
            typeof(Utf8ColumnParserFactory).GetMethod(nameof(BuildParsable), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo BuildConverterMethod =
            typeof(Utf8ColumnParserFactory).GetMethod(nameof(BuildConverter), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo BuildDoubleMethod =
            typeof(Utf8ColumnParserFactory).GetMethod(nameof(BuildDouble), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo BuildFloatMethod =
            typeof(Utf8ColumnParserFactory).GetMethod(nameof(BuildFloat), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo BuildNullableMethod =
            typeof(Utf8ColumnParserFactory).GetMethod(nameof(BuildNullable), BindingFlags.NonPublic | BindingFlags.Static)!;

        /// <summary>
        /// Creates a <see cref="Utf8ColumnParser{TModel}"/> for the given member type and setter. The
        /// setter is a compiled expression assigning the parsed value to the member.
        /// When <paramref name="valueType"/> is a <see cref="Nullable{T}"/>, a blank (trimmed-empty) column assigns
        /// <see langword="null"/> without invoking the underlying parser; otherwise resolution for the underlying <c>T</c>
        /// proceeds exactly as for a non-nullable column. When <paramref name="converterType"/> is set (from
        /// <c>FixedColumnAttribute.Converter</c>), it takes priority over the <see cref="IUtf8SpanParsable{TSelf}"/> fallback.
        /// </summary>
        /// <remarks><paramref name="setter"/> must be a <c>RefAction&lt;TModel, valueType&gt;</c> assigning the parsed value to the member.</remarks>
        public static Utf8ColumnParser<TModel> Create<TModel>(Type valueType, Delegate setter, Type? converterType, string memberName, char trimChar = ' ', Attributes.TrimMode trimMode = Attributes.TrimMode.Trailing, string? format = null)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            byte trimByte = Utf8FixedWidthRuntime.ToAsciiByte(trimChar, memberName);
            var underlyingType = Nullable.GetUnderlyingType(valueType);
            if (underlyingType is not null)
            {
                return (Utf8ColumnParser<TModel>)BuildNullableMethod.MakeGenericMethod(typeof(TModel), underlyingType)
                                                                    .Invoke(null, [setter, converterType, memberName, trimByte, trimMode, format])!;
            }

            return ColumnParserFactoryShared.Create<TModel, Utf8ColumnParser<TModel>>(
                valueType, setter, converterType, memberName, trimByte, trimMode, format, typeof(IUtf8FixedWidthConverter<>),
                BuildParsableMethod, BuildConverterMethod, BuildDoubleMethod, BuildFloatMethod, BuildString, BuildExact<TModel>);
        }

        // A nullable column (T?): a blank (trimmed-empty) column assigns null without invoking the
        // underlying parser. Otherwise, the underlying T resolves exactly as a non-nullable column
        // would (converter → double/float → IUtf8SpanParsable fallback), via an adapter setter that
        // boxes the result into the T? setter — no duplicated parse logic per type.
        private static Utf8ColumnParser<TModel> BuildNullable<TModel, TUnderlying>(Delegate setter, Type? converterType, string memberName, byte trimChar, Attributes.TrimMode trimMode, string? format)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
            where TUnderlying : struct
        {
            var nullableSetter = (RefAction<TModel, TUnderlying?>)setter;
            RefAction<TModel, TUnderlying> adapted = (ref TModel model, TUnderlying value) => nullableSetter(ref model, value);

            var inner = ColumnParserFactoryShared.Create<TModel, Utf8ColumnParser<TModel>>(
                typeof(TUnderlying), adapted, converterType, memberName, trimChar, trimMode, format, typeof(IUtf8FixedWidthConverter<>),
                BuildParsableMethod, BuildConverterMethod, BuildDoubleMethod, BuildFloatMethod, BuildString, BuildExact<TModel>);

            return (column, formatProvider, stringPool, ref model) =>
            {
                if (column.Trim(trimChar).Trim((byte)' ').IsEmpty)
                {
                    nullableSetter(ref model, null);
                    return true;
                }
                return inner(column, formatProvider, stringPool, ref model);
            };
        }

        // Fallback for types with no dedicated builder: call the static-abstract IUtf8SpanParsable.TryParse
        // directly inside the column closure. This is a constrained call the JIT devirtualizes to the
        // concrete TValue.TryParse (and can inline), saving one indirect call per column and no
        // value-parser delegate allocated.
        private static Utf8ColumnParser<TModel> BuildParsable<TModel, TValue>(RefAction<TModel, TValue> setter, byte trimChar, Attributes.TrimMode trimMode)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
            where TValue : IUtf8SpanParsable<TValue>
        {
            return (column, formatProvider, _, ref model) =>
            {
                // Null means invariant (matches Utf8FixedWidthRuntime.TryParse<TValue> and the double/float
                // columns) rather than the BCL's own CurrentCulture default.
                if (!TValue.TryParse(Utf8FixedWidthRuntime.TrimColumn(column, trimChar, trimMode), formatProvider ?? CultureInfo.InvariantCulture, out var value))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static Utf8ColumnParser<TModel> BuildDouble<TModel>(RefAction<TModel, double> setter, byte trimChar, Attributes.TrimMode trimMode)
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

        private static Utf8ColumnParser<TModel> BuildFloat<TModel>(RefAction<TModel, float> setter, byte trimChar, Attributes.TrimMode trimMode)
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
        private static Utf8ColumnParser<TModel> BuildConverter<TModel, TValue, TConverter>(
            RefAction<TModel, TValue> setter, TConverter converter, byte trimChar, Attributes.TrimMode trimMode)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
            where TConverter : IUtf8FixedWidthConverter<TValue>
        {
            return (column, formatProvider, _, ref model) =>
            {
                if (!converter.TryParse(Utf8FixedWidthRuntime.TrimColumn(column, trimChar, trimMode), formatProvider, out var value))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static Utf8ColumnParser<TModel> BuildString<TModel>(RefAction<TModel, string> setter, object trimChar, Attributes.TrimMode trimMode)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            byte trim = (byte)trimChar;
            return (column, _, stringPool, ref model) =>
            {
                var slice = Utf8FixedWidthRuntime.TrimColumn(column, trim, trimMode);
                setter(ref model, stringPool is null ? Encoding.UTF8.GetString(slice) : stringPool.GetOrAdd(slice, Encoding.UTF8));
                return true;
            };
        }

        private static Utf8ColumnParser<TModel> BuildExact<TModel>(Type valueType, Delegate setter, string format, object trimChar, Attributes.TrimMode trimMode)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            byte trim = (byte)trimChar;
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

        private static Utf8ColumnParser<TModel> BuildExact<TModel, TValue>(RefAction<TModel, TValue> setter, string format, byte trim, Attributes.TrimMode trimMode, ExactParser<TValue> parser)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            return (column, fp, _, ref model) =>
            {
                var trimmed = Utf8FixedWidthRuntime.TrimColumn(column, trim, trimMode);
                Span<char> chars = trimmed.Length <= 128 ? stackalloc char[128] : new char[trimmed.Length];
                int written = Encoding.UTF8.GetChars(trimmed, chars);
                if (!parser(chars[..written], format, fp ?? CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }
    }
}
