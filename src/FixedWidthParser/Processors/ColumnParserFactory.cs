using System.Diagnostics.CodeAnalysis;
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
        private static readonly MethodInfo CreateParsableValueParserMethod =
            typeof(ColumnParserFactory).GetMethod(nameof(CreateParsableValueParser), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo BuildGenericMethod =
            typeof(ColumnParserFactory).GetMethod(nameof(BuildGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;

        /// <remarks><paramref name="setter"/> must be a <c>RefAction&lt;TModel, valueType&gt;</c> assigning the parsed value to the member.</remarks>
        public static ColumnParser<TModel> Create<TModel>(Type valueType, Delegate setter)
            where TModel : allows ref struct
        {
            if (valueType == typeof(string))
            {
                return BuildString((RefAction<TModel, string>)setter);
            }
            var valueParser = ColumnParserRegistry.Get(valueType) ?? (Delegate)CreateParsableValueParserMethod.MakeGenericMethod(valueType).Invoke(null, null)!;
            return (ColumnParser<TModel>)BuildGenericMethod.MakeGenericMethod(typeof(TModel), valueType)
                                                           .Invoke(null, [setter, valueParser])!;
        }

        private static ColumnValueParser<TValue> CreateParsableValueParser<TValue>() where TValue : ISpanParsable<TValue>
            => static (span, formatProvider, [MaybeNullWhen(false)] out value)
                => TValue.TryParse(span.TrimEnd(' '), formatProvider, out value);

        private static ColumnParser<TModel> BuildGeneric<TModel, TValue>(
            RefAction<TModel, TValue> setter, ColumnValueParser<TValue> valueParser)
            where TModel : allows ref struct
        {
            return (column, formatProvider, stringPool, ref model) =>
            {
                if (!valueParser(column, formatProvider, out var value))
                {
                    return false;
                }
                setter(ref model, value);
                return true;
            };
        }

        private static ColumnParser<TModel> BuildString<TModel>(RefAction<TModel, string> setter)
            where TModel : allows ref struct
        {
            return (column, formatProvider, stringPool, ref model) =>
            {
                var slice = column.TrimEnd(' ');
                setter(ref model, stringPool is null ? slice.ToString() : stringPool.GetOrAdd(slice));
                return true;
            };
        }
    }
}
