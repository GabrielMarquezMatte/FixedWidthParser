using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser
{
    /// <summary>
    /// UTF-8 / byte counterpart of <see cref="IFixedWidthModel{TSelf}"/>: implemented by models that
    /// opt into the compile-time generated <em>byte</em> parser. Declaring this interface on a
    /// <see langword="partial"/> model is the trigger for the source generator, which fills in
    /// <see cref="TryParse"/> with fully-inlined, reflection-free parsing straight from raw UTF-8 bytes
    /// (mirroring the runtime <see cref="Parsers.Utf8FixedWidthParser{TModel}"/> semantics). A model may
    /// implement this, <see cref="IFixedWidthModel{TSelf}"/>, or both — the two generated
    /// <c>TryParse</c> methods are distinct overloads (one over <see cref="char"/>, one over
    /// <see cref="byte"/>) and coexist in the same partial type.
    /// <para><b>Column offsets are measured in bytes</b>, exact for the single-byte/ASCII payloads
    /// typical of flat files (see <see cref="Parsers.Utf8FixedWidthParser{TModel}"/>).</para>
    /// </summary>
    public interface IUtf8FixedWidthModel<TSelf> where TSelf : IUtf8FixedWidthModel<TSelf>, allows ref struct
    {
        /// <summary>
        /// Parses a single UTF-8 fixed-width line into <paramref name="model"/>. Returns
        /// <see langword="false"/> (rejecting the line) when the line is shorter (in bytes) than the
        /// layout or a non-string column fails to parse.
        /// </summary>
        static abstract bool TryParse(ReadOnlySpan<byte> line, IFormatProvider? formatProvider, StringPool? stringPool, out TSelf model);
    }
}
