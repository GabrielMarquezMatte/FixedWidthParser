namespace FixedWidthParser
{
    /// <summary>
    /// Implemented by models that opt into the compile-time generated parser and writer. Declaring
    /// this interface on a <see langword="partial"/> model is the trigger for the source generator,
    /// which fills in <see cref="TryParse"/> and <see cref="TryFormat"/> with reflection-free code
    /// that reads and writes exactly like the model's <see cref="FixedWidthLayout.FromAttributes{TModel}"/> layout.
    /// <para><b>Column positions are bytes.</b></para>
    /// </summary>
    public interface IFixedWidthModel<TSelf> where TSelf : IFixedWidthModel<TSelf>, allows ref struct
    {
        /// <summary>
        /// Parses a single fixed-width line, without its line break, into <paramref name="model"/>.
        /// Returns <see langword="false"/> (rejecting the line) when the line is shorter than the layout
        /// or a non-string column fails to parse, after reporting why through
        /// <see cref="FixedWidthContext.ReportFailedColumn"/> or <see cref="FixedWidthContext.ReportShortLine"/>.
        /// </summary>
        static abstract bool TryParse(ReadOnlySpan<byte> line, in FixedWidthContext context, out TSelf model);

        /// <summary>
        /// Formats <paramref name="model"/> into <paramref name="destination"/>, without a line break,
        /// mirroring the model's attribute layout. Returns <see langword="false"/> only
        /// when <paramref name="destination"/> is shorter than the line; a value that does not fit its
        /// column throws or truncates per its <c>FixedColumnAttribute.Overflow</c>.
        /// </summary>
        static abstract bool TryFormat(in TSelf model, Span<byte> destination, in FixedWidthContext context, out int bytesWritten);
    }
}
