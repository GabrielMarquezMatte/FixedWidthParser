using System.Reflection;
using System.Text;
using csFastFloat;

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
        public static Utf8ColumnParser<TModel> Create<TModel>(Type valueType, Delegate setter, Type? converterType, string memberName)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            var underlyingType = Nullable.GetUnderlyingType(valueType);
            if (underlyingType is not null)
            {
                return (Utf8ColumnParser<TModel>)BuildNullableMethod.MakeGenericMethod(typeof(TModel), underlyingType)
                                                                    .Invoke(null, [setter, converterType, memberName])!;
            }

            return ColumnParserFactoryShared.Create<TModel, Utf8ColumnParser<TModel>>(
                valueType, setter, converterType, memberName, typeof(IUtf8FixedWidthConverter<>),
                BuildParsableMethod, BuildConverterMethod, BuildDoubleMethod, BuildFloatMethod, BuildString);
        }

        // A nullable column (T?): a blank (trimmed-empty) column assigns null without invoking the
        // underlying parser. Otherwise, the underlying T resolves exactly as a non-nullable column
        // would (converter → double/float → IUtf8SpanParsable fallback), via an adapter setter that
        // boxes the result into the T? setter — no duplicated parse logic per type.
        private static Utf8ColumnParser<TModel> BuildNullable<TModel, TUnderlying>(Delegate setter, Type? converterType, string memberName)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
            where TUnderlying : struct
        {
            var nullableSetter = (RefAction<TModel, TUnderlying?>)setter;
            RefAction<TModel, TUnderlying> adapted = (ref TModel model, TUnderlying value) => nullableSetter(ref model, value);

            var inner = ColumnParserFactoryShared.Create<TModel, Utf8ColumnParser<TModel>>(
                typeof(TUnderlying), adapted, converterType, memberName, typeof(IUtf8FixedWidthConverter<>),
                BuildParsableMethod, BuildConverterMethod, BuildDoubleMethod, BuildFloatMethod, BuildString);

            return (column, formatProvider, stringPool, ref model) =>
            {
                if (column.TrimEnd((byte)' ').IsEmpty)
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
        private static Utf8ColumnParser<TModel> BuildParsable<TModel, TValue>(RefAction<TModel, TValue> setter)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
            where TValue : IUtf8SpanParsable<TValue>
        {
            return (column, formatProvider, _, ref model) =>
            {
                if (!TValue.TryParse(column.TrimEnd((byte)' '), formatProvider, out var value))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static Utf8ColumnParser<TModel> BuildDouble<TModel>(RefAction<TModel, double> setter)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            return (column, formatProvider, _, ref model) =>
            {
                if (!FastDoubleParser.TryParseDouble(column.TrimEnd((byte)' '), out double value, decimal_separator: CultureHelpers.GetDecimalSeparatorByte(formatProvider)))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static Utf8ColumnParser<TModel> BuildFloat<TModel>(RefAction<TModel, float> setter)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            return (column, formatProvider, _, ref model) =>
            {
                if (!FastFloatParser.TryParseFloat(column.TrimEnd((byte)' '), out float value, decimal_separator: CultureHelpers.GetDecimalSeparatorByte(formatProvider)))
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
            RefAction<TModel, TValue> setter, TConverter converter)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
            where TConverter : IUtf8FixedWidthConverter<TValue>
        {
            return (column, formatProvider, _, ref model) =>
            {
                if (!converter.TryParse(column.TrimEnd((byte)' '), formatProvider, out var value))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static Utf8ColumnParser<TModel> BuildString<TModel>(RefAction<TModel, string> setter)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            return (column, _, stringPool, ref model) =>
            {
                var slice = column.TrimEnd((byte)' ');
                setter(ref model, stringPool is null ? Encoding.UTF8.GetString(slice) : stringPool.GetOrAdd(slice, Encoding.UTF8));
                return true;
            };
        }
    }
}
