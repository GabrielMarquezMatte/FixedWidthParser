using System.Reflection;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Processors
{
    /// <summary>
    /// Combines a column's compiled setter and a value parser into a single
    /// <see cref="ColumnParser{TModel}"/> closure. The column span is sliced by the parser and passed
    /// in, so these closures only parse and assign. The value parser comes from
    /// <see cref="ColumnParserRegistry"/> or, when absent, from the <see cref="ISpanParsable{TSelf}"/>
    /// fallback. <c>string</c> is special-cased (it never fails and uses the <see cref="StringPool"/>).
    /// </summary>
    internal static class ColumnParserFactory
    {
        private static readonly MethodInfo BuildParsableMethod =
            typeof(ColumnParserFactory).GetMethod(nameof(BuildParsable), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo BuildGenericMethod =
            typeof(ColumnParserFactory).GetMethod(nameof(BuildGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;

        /// <summary>
        /// Creates a <see cref="ColumnParser{TModel}"/> for the given member type and setter. The setter is a compiled expression assigning the parsed value to the member. The value parser is resolved from 
        /// <see cref="ColumnParserRegistry"/> or, when absent, from the <see cref="ISpanParsable{TSelf}"/> fallback.
        /// </summary>
        /// <remarks><paramref name="setter"/> must be a <c>RefAction&lt;TModel, valueType&gt;</c> assigning the parsed value to the member.</remarks>
        public static ColumnParser<TModel> Create<TModel>(Type valueType, Delegate setter)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            return ColumnParserFactoryShared.Create<TModel, ColumnParser<TModel>>(
                valueType, setter, ColumnParserRegistry.Get, BuildParsableMethod, BuildGenericMethod, BuildString);
        }

        // Fallback for types not in the registry: call the static-abstract ISpanParsable.TryParse
        // directly inside the column closure. This is a constrained call the JIT devirtualizes to the
        // concrete TValue.TryParse (and can inline), saving one indirect call per column versus routing
        // through a ColumnValueParser delegate — and no such delegate is allocated. Registered (custom)
        // parsers are runtime delegates and still go through BuildGeneric.
        private static ColumnParser<TModel> BuildParsable<TModel, TValue>(RefAction<TModel, TValue> setter)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
            where TValue : ISpanParsable<TValue>
        {
            return (column, formatProvider, _, ref model) =>
            {
                if (!TValue.TryParse(column.TrimEnd(' '), formatProvider, out var value))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static ColumnParser<TModel> BuildGeneric<TModel, TValue>(
            RefAction<TModel, TValue> setter, ColumnValueParser<TValue> valueParser)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            return (column, formatProvider, _, ref model) =>
            {
                if (!valueParser(column.TrimEnd(' '), formatProvider, out var value))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static ColumnParser<TModel> BuildString<TModel>(RefAction<TModel, string> setter)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            return (column, _, stringPool, ref model) =>
            {
                var slice = column.TrimEnd(' ');
                setter(ref model, stringPool is null ? slice.ToString() : stringPool.GetOrAdd(slice));
                return true;
            };
        }
    }
}
