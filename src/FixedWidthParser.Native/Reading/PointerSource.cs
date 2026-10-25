using FixedWidthParser.Readers;

namespace FixedWidthParser.Native.Reading
{
    /// <summary>The caller's memory as a random-access source. The memory must stay valid while it is read.</summary>
    internal sealed unsafe class PointerSource : IRandomAccessSource
    {
        private readonly byte* _data;

        internal PointerSource(byte* data, long length)
        {
            _data = data;
            Length = length;
        }

        public long Length { get; }

        public int Read(Span<byte> destination, long offset)
        {
            if (offset >= Length)
            {
                return 0;
            }
            int count = (int)Math.Min(destination.Length, Length - offset);
            new ReadOnlySpan<byte>(_data + offset, count).CopyTo(destination);
            return count;
        }
    }
}
