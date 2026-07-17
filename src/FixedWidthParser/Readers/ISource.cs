namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Synchronous and asynchronous source for buffered record reading. Implemented by value types
    /// so the reader cores specialize the calls without interface dispatch or allocations.
    /// </summary>
    public interface ISource<T> where T : unmanaged
    {
        /// <summary>Reads buffered elements into <paramref name="buffer"/>.</summary>
        int Read(Span<T> buffer);

        /// <summary>Asynchronously reads buffered elements into <paramref name="buffer"/>.</summary>
        ValueTask<int> ReadAsync(Memory<T> buffer, CancellationToken cancellationToken);

        /// <summary>Disposes the source when it owns its underlying resource.</summary>
        void Dispose();
    }
}
