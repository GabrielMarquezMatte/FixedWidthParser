using Microsoft.Win32.SafeHandles;

namespace FixedWidthParser.Readers
{
    /// <summary>Bytes read at an offset, from any thread: what the parallel reader partitions.</summary>
    internal interface IRandomAccessSource
    {
        long Length { get; }

        /// <summary>Copies the bytes at <paramref name="offset"/> into <paramref name="destination"/>; 0 at the end.</summary>
        int Read(Span<byte> destination, long offset);
    }

    internal sealed class FileSource(SafeFileHandle handle) : IRandomAccessSource
    {
        public long Length { get; } = RandomAccess.GetLength(handle);

        public int Read(Span<byte> destination, long offset)
        {
            return RandomAccess.Read(handle, destination, offset);
        }
    }
}
