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
    /// and is decoded with <see cref="Encoding.UTF8"/>, interned through the supplied
    /// <see cref="CommunityToolkit.HighPerformance.Buffers.StringPool"/> when one is provided.
    /// </summary>
    internal static class Utf8ColumnParserFactory
    {
        private static readonly MethodInfo BuildParsableMethod =
            typeof(Utf8ColumnParserFactory).GetMethod(nameof(BuildParsable), BindingFlags.NonPublic | BindingFlags.Static)!;
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
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            return ColumnParserFactoryShared.Create<TModel, Utf8ColumnParser<TModel>>(
                valueType, setter, Utf8ColumnParserRegistry.Get, BuildParsableMethod, BuildGenericMethod, BuildString);
        }

        // Fallback for types not in the registry: call the static-abstract IUtf8SpanParsable.TryParse
        // directly inside the column closure. This is a constrained call the JIT devirtualizes to the
        // concrete TValue.TryParse (and can inline), saving one indirect call per column versus routing
        // through a Utf8ColumnValueParser delegate — and no such delegate is allocated. Registered
        // (custom) parsers are runtime delegates and still go through BuildGeneric.
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

        private static Utf8ColumnParser<TModel> BuildGeneric<TModel, TValue>(
            RefAction<TModel, TValue> setter, Utf8ColumnValueParser<TValue> valueParser)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            return (column, formatProvider, _, ref model) =>
            {
                if (!valueParser(column.TrimEnd((byte)' '), formatProvider, out var value))
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
