using System.Buffers;
using FixedWidthParser.Native.Layouts;
using FixedWidthParser.Readers;

namespace FixedWidthParser.Native.Reading
{
    /// <summary>
    /// The final buffers of a whole-source read, lent a slice per partition so the partitions parse straight into them
    /// and publishing has nothing to copy. Every line is taken to be as long as the first, so a partition's first row
    /// is its offset over that length. A partition with fewer rows leaves a gap that publishing closes in place; one
    /// with more copies its column out of the slice, and that column is copied as before. A STRING column is lent
    /// room for its whole width on every row, for its bytes, and its offsets: a slice's index 0 is the previous slice's
    /// last offset, so only the first slice writes the column's leading 0.
    /// </summary>
    internal sealed unsafe class TableArena : IDisposable
    {
        private const int ProbeBytes = 64 * 1024;

        private readonly long _dataStart;
        private readonly long _stride;
        private readonly int _rows;
        private readonly int[] _sizes;
        private readonly GrowableNativeBuffer<byte>?[] _blocks;
        private readonly GrowableNativeBuffer<int>?[] _offsetBlocks;

        private TableArena(long dataStart, long stride, int rows, NativeLayout layout, TableLimits limits)
        {
            _dataStart = dataStart;
            _stride = stride;
            _rows = rows;
            _sizes = [.. layout.Columns.Select(c => BytesPerRow(c, rows, limits))];
            _blocks = new GrowableNativeBuffer<byte>?[_sizes.Length];
            for (int i = 0; i < _blocks.Length; i++)
            {
                if (_sizes[i] > 0)
                {
                    var block = new GrowableNativeBuffer<byte>();
                    block.Reserve((long)rows * _sizes[i]);
                    _blocks[i] = block;
                }
            }
            _offsetBlocks = new GrowableNativeBuffer<int>?[_sizes.Length];
            for (int i = 0; i < _offsetBlocks.Length; i++)
            {
                if (layout.Columns[i].Type == NativeType.String)
                {
                    var offsets = new GrowableNativeBuffer<int>();
                    offsets.Reserve(rows + 1L);
                    _offsetBlocks[i] = offsets;
                }
            }
        }

        /// <summary>
        /// An arena sized from the length of the first line at <paramref name="dataStart"/>; null when there is no line
        /// break in the first 64 KiB, or the rows would not fit one buffer or <paramref name="limits"/>.
        /// </summary>
        internal static TableArena? TryCreate(IRandomAccessSource source, long dataStart, NativeLayout layout, TableLimits limits)
        {
            byte[] probe = ArrayPool<byte>.Shared.Rent(ProbeBytes);
            try
            {
                int read = source.Read(probe.AsSpan(0, ProbeBytes), dataStart);
                long stride = probe.AsSpan(0, read).IndexOf((byte)'\n') + 1L;
                if (stride == 0)
                {
                    return null;
                }
                long rows = (source.Length - dataStart + stride - 1) / stride;
                bool fits = rows > 0 && rows <= limits.MaxRows && rows * sizeof(long) <= int.MaxValue;
                return fits ? new TableArena(dataStart, stride, (int)rows, layout, limits) : null;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(probe);
            }
        }

        /// <summary>What the arena lends a column per row; 0 for a STRING column whose full width would break the text limit.</summary>
        private static int BytesPerRow(NativeColumn column, int rows, TableLimits limits)
        {
            if (column.Type != NativeType.String)
            {
                return ColumnBuilder.ValueSize(column.Type);
            }
            return (long)rows * column.Length <= Math.Min(limits.MaxTextBytes, int.MaxValue) ? column.Length : 0;
        }

        /// <summary>The row of the first line starting at or after <paramref name="offset"/>, if every line is as long as the first.</summary>
        internal int RowAt(long offset)
        {
            return (int)Math.Min(_rows, (offset - _dataStart + _stride - 1) / _stride);
        }

        /// <summary>Where column <paramref name="column"/>'s value for <paramref name="row"/> goes; null for a column the arena does not hold.</summary>
        internal void* Lend(int column, int row)
        {
            return _blocks[column] is { } block ? block.Pointer + ((long)row * _sizes[column]) : null;
        }

        internal GrowableNativeBuffer<byte>? Block(int column)
        {
            return _blocks[column];
        }

        /// <summary>Where a STRING column's offset at index <paramref name="row"/> goes, the start of the row's text; null for other columns.</summary>
        internal int* LendOffsets(int column, int row)
        {
            return _offsetBlocks[column] is { } block ? block.Pointer + row : null;
        }

        internal GrowableNativeBuffer<int>? OffsetsBlock(int column)
        {
            return _offsetBlocks[column];
        }

        /// <summary>Where a STRING column's offset at index <paramref name=

        public void Dispose()
        {
            foreach (var block in _blocks)
            {
                block?.Dispose();
            }
            foreach (var block in _offsetBlocks)
            {
                block?.Dispose();
            }
        }
    }
}
