using System.Reflection;

namespace FixedWidthParser.Processors
{
    /// <summary>
    /// Shared build-time orchestration for <see cref="ColumnParserFactory"/> (char) and
    /// <see cref="Utf8ColumnParserFactory"/> (byte): the identical "string-special-case → registry
    /// lookup → <see cref="ISpanParsable{TSelf}"/>/<see cref="IUtf8SpanParsable{TSelf}"/> fallback →
    /// wrap setter+parser" flow. The element-specific pieces (string builder, registry, the reflected
    /// value-parser and wrapper methods) are passed in, so the spans-of-different-element-type closures
    /// stay in each factory while the reflection plumbing lives in one place.
    /// </summary>
    internal static class ColumnParserFactoryShared
    {
        public static TColumnParser Create<TModel, TColumnParser>(
            Type valueType,
            Delegate setter,
            Func<Type, Delegate?> registryGet,
            MethodInfo createParsableValueParserMethod,
            MethodInfo buildGenericMethod,
            Func<RefAction<TModel, string>, TColumnParser> buildString)
            where TModel : allows ref struct
            where TColumnParser : Delegate
        {
            if (valueType == typeof(string))
            {
                return buildString((RefAction<TModel, string>)setter);
            }

            var valueParser = registryGet(valueType)
                ?? (Delegate)createParsableValueParserMethod.MakeGenericMethod(valueType).Invoke(null, null)!;
            return (TColumnParser)buildGenericMethod.MakeGenericMethod(typeof(TModel), valueType)
                                                    .Invoke(null, [setter, valueParser])!;
        }
    }
}
