using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser
{
    /// <summary>
    /// Implemented by models that opt into the compile-time generated parser. Declaring this
    /// interface on a <see langword="partial"/> model is the trigger for the source generator, which
    /// fills in <see cref="TryParse"/> with fully-inlined, reflection-free parsing (mirroring the
    /// runtime <see cref="Parsers.FixedWidthParser{TModel}"/> semantics). Mirrors the shape of
    /// <see cref="ISpanParsable{TSelf}"/> so a generic, devirtualized read path is possible.
    /// </summary>
#if NET9_0_OR_GREATER
    public interface IFixedWidthModel<TSelf> where TSelf : IFixedWidthModel<TSelf>, allows ref struct
#else
    public interface IFixedWidthModel<TSelf> where TSelf : IFixedWidthModel<TSelf>
#endif
    {
        /// <summary>
        /// Parses a single fixed-width line into <paramref name="model"/>. Returns
        /// <see langword="false"/> (rejecting the line) when a non-string column fails to parse.
        /// </summary>
        static abstract bool TryParse(ReadOnlySpan<char> line, IFormatProvider? formatProvider, StringPool? stringPool, out TSelf model);
    }
}
