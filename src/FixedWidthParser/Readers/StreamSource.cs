namespace FixedWidthParser.Readers
{
    /// <summary>Stream-backed byte source for the shared record enumerator cores.</summary>
    public readonly struct StreamSource(Stream stream, bool ownsStream) : ISource<byte>
    {
        /// <inheritdoc />
        public int Read(Span<byte> buffer)
        {
            return stream.Read(buffer);
        }

        /// <inheritdoc />
        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            return stream.ReadAsync(buffer, cancellationToken);
        }

        /// <inheritdoc />
        public void Dispose()
        {
#pragma warning disable IDISP007 // Don't dispose injected
            if (ownsStream)
            {
                stream.Dispose();
            }
#pragma warning restore IDISP007 // Don't dispose injected
        }
    }
}
