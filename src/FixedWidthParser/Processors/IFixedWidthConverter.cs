namespace FixedWidthParser.Processors
{
    /// <summary>
    /// Per-property custom conversion for a fixed-width column, wired via
    /// <c>FixedColumnAttribute.Converter</c>. A single instance is created once (when the owning
    /// model's parser/writer is built) and reused for every row, so implementations must be stateless.
    /// One interface covers both directions: a fixed-width column always round-trips.
    /// </summary>
    public interface IFixedWidthConverter<T>
    {
        /// <summary>Parses the already-sliced, trimmed column text. Returns <see langword="false"/> to reject the line.</summary>
        bool TryParse(ReadOnlySpan<char> field, IFormatProvider? formatProvider, out T value);

        /// <summary>Formats <paramref name="value"/> into <paramref name="destination"/>, returning the number of characters written.</summary>
        bool TryFormat(T value, Span<char> destination, IFormatProvider? formatProvider, out int written);
    }

    /// <summary>UTF-8 counterpart of <see cref="IFixedWidthConverter{T}"/>, used by the byte reader/writer.</summary>
    public interface IUtf8FixedWidthConverter<T>
    {
        /// <summary>Parses the already-sliced, trimmed column bytes. Returns <see langword="false"/> to reject the line.</summary>
        bool TryParse(ReadOnlySpan<byte> field, IFormatProvider? formatProvider, out T value);

        /// <summary>Formats <paramref name="value"/> into <paramref name="destination"/>, returning the number of bytes written.</summary>
        bool TryFormat(T value, Span<byte> destination, IFormatProvider? formatProvider, out int written);
    }
}
