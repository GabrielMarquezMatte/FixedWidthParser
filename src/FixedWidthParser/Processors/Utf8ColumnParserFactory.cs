using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

namespace FixedWidthParser.Processors
{
    /// <summary>
    /// UTF-8 counterpart of <see cref="ColumnParserFactory"/>: combines a column's compiled setter and
    /// a UTF-8 value parser into a single <see cref="Utf8ColumnParser{TModel}"/> closure. The column
    /// span is sliced (in bytes) by the parser and passed in, so these closures only parse and assign.
    /// The value parser comes from <see cref="Utf8ColumnParserRegistry"/> or, when absent, from the
    /// <see cref="IUtf8SpanParsable{TSelf}"/> fallback. <c>string</c> is special-cased: it never fails
    /// and is decoded with <see cref="Encoding.UTF8"/> (the byte reader does not pool strings).
    /// </summary>
    internal static class Utf8ColumnParserFactory
    {
        private static readonly MethodInfo CreateParsableValueParserMethod =
            typeof(Utf8ColumnParserFactory).GetMethod(nameof(CreateParsableValueParser), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo BuildGenericMethod =
            typeof(Utf8ColumnParserFactory).GetMethod(nameof(BuildGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;

        /// <summary>
        /// Creates a <see cref="Utf8ColumnParser{TModel}"/> for the given member type and setter. The
        /// setter is a compiled expression assigning the parsed value to the member. The value parser
        /// is resolved from <see cref="Utf8ColumnParserRegistry"/> or, when absent, from the
        /// <see cref="IUtf8SpanParsable{TSelf}"/> fallback.
        /// </summary>
        /// <remarks><paramref name="setter"/> must be a <c>RefAction&lt;TModel, valueType&gt;</c> assigning the parsed value to the member.</remarks>
        public static Utf8ColumnParser<TModel> Create<TModel>(Type valueType, Delegate setter)
            where TModel : allows ref struct
        {
            if (valueType == typeof(string))
            {
                return BuildString((RefAction<TModel, string>)setter);
            }
            var valueParser = Utf8ColumnParserRegistry.Get(valueType) ?? (Delegate)CreateParsableValueParserMethod.MakeGenericMethod(valueType).Invoke(null, null)!;
            return (Utf8ColumnParser<TModel>)BuildGenericMethod.MakeGenericMethod(typeof(TModel), valueType)
                                                               .Invoke(null, [setter, valueParser])!;
        }

        private static Utf8ColumnValueParser<TValue> CreateParsableValueParser<TValue>() where TValue : IUtf8SpanParsable<TValue>
        {
            return static (span, formatProvider, [MaybeNullWhen(false)] out value)
                        => TValue.TryParse(span.TrimEnd((byte)' '), formatProvider, out value);
        }

        private static Utf8ColumnParser<TModel> BuildGeneric<TModel, TValue>(
            RefAction<TModel, TValue> setter, Utf8ColumnValueParser<TValue> valueParser)
            where TModel : allows ref struct
        {
            return (column, formatProvider, ref model) =>
            {
                if (!valueParser(column, formatProvider, out var value))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static Utf8ColumnParser<TModel> BuildString<TModel>(RefAction<TModel, string> setter)
            where TModel : allows ref struct
        {
            return (column, _, ref model) =>
            {
                var slice = column.TrimEnd((byte)' ');
                setter(ref model, Encoding.UTF8.GetString(slice));
                return true;
            };
        }
    }
}
