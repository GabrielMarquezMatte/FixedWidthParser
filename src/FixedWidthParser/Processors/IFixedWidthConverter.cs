namespace FixedWidthParser.Processors
{
    /// <summary>
    /// Per-property custom conversion for a fixed-width column, wired via
    /// <c>FixedColumnAttribute.Converter</c>. A single instance is created once (when the owning
    /// model's parser/writer is built) and reused for every row, so implementations must be stateless.
    /// One interface covers both directions: a fixed-width column always round-trips.
    /// </summary>
    /// <remarks>A later version never adds a member to this interface; new settings arrive on <see cref="FixedWidthContext"/>.</remarks>
    public interface IFixedWidthConverter<T>
    {
        /// <summary>
        /// Parses the already-sliced, trimmed column bytes. Decode text with
        /// <see cref="FixedWidthContext.Encoding"/>. Returns <see langword="false"/> to reject the line.
        /// </summary>
        bool TryParse(ReadOnlySpan<byte> field, in FixedWidthContext context, out T value);

        /// <summary>
        /// Formats <paramref name="value"/> into <paramref name="destination"/>, encoding text with
        /// <see cref="FixedWidthContext.Encoding"/>, and returns the number of bytes written. Returns
        /// <see langword="false"/> when <paramref name="destination"/> is too small; the writer retries
        /// with a larger buffer.
        /// </summary>
        bool TryFormat(T value, Span<byte> destination, in FixedWidthContext context, out int written);
    }
}
