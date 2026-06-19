using System.Reflection;

namespace FixedWidthParser.Processors
{
    /// <summary>
    /// Shared build-time orchestration for <see cref="ColumnParserFactory"/> (char) and
    /// <see cref="Utf8ColumnParserFactory"/> (byte): the identical "string-special-case → registry
    /// lookup → <see cref="ISpanParsable{TSelf}"/>/<see cref="IUtf8SpanParsable{TSelf}"/> fallback →
    /// wrap setter+parser" flow. The element-specific pieces (string builder, registry, the reflected
    /// build-parsable and wrapper methods) are passed in, so the spans-of-different-element-type closures
    /// stay in each factory while the reflection plumbing lives in one place.
    /// </summary>
    internal static class ColumnParserFactoryShared
    {
        /// <summary>
        /// <para>
        /// A registered (custom) value parser is a runtime delegate, so it is wrapped together with the
        /// setter by <paramref name="buildGenericMethod"/> (closure → value-parser delegate → setter). The
        /// unregistered fallback, by contrast, calls the static-abstract <c>TryParse</c> directly inside the
        /// column closure via <paramref name="buildParsableMethod"/> — one fewer indirect call per column
        /// and no value-parser delegate allocated.
        /// </para>
        /// </summary>
        public static TColumnParser Create<TModel, TColumnParser>(
            Type valueType,
            Delegate setter,
            Func<Type, Delegate?> registryGet,
            MethodInfo buildParsableMethod,
            MethodInfo buildGenericMethod,
            Func<RefAction<TModel, string>, TColumnParser> buildString)
            where TModel : allows ref struct
            where TColumnParser : Delegate
        {
            if (valueType == typeof(string))
            {
                return buildString((RefAction<TModel, string>)setter);
            }

            var valueParser = registryGet(valueType);
            if (valueParser is not null)
            {
                return (TColumnParser)buildGenericMethod.MakeGenericMethod(typeof(TModel), valueType)
                                                        .Invoke(null, [setter, valueParser])!;
            }

            return (TColumnParser)buildParsableMethod.MakeGenericMethod(typeof(TModel), valueType)
                                                     .Invoke(null, [setter])!;
        }
    }
}
