using System.Runtime.InteropServices;

namespace FixedWidthParser.Native.Arrow
{
    /// <summary>
    /// Dictionary-encodes one STRING column for Arrow: an int32 index per row into the column's distinct values, in
    /// the order they first appear. A null row keeps its index to the empty string; its validity bit says it is null.
    /// </summary>
    // ponytail: encodes the finished column at export, one thread per column: ~30 ms on COTAHIST's 1M rows with
    // nine dictionary columns, bound by the most varied one. Encoding each partition while it parses, then
    // remapping the partitions' indices at publish, would spread that over every thread.
    internal sealed unsafe class DictionaryEncoder : IDisposable
    {
        private const int MinSlots = 1024;

        private readonly GrowableNativeBuffer<int> _offsets = new();
        private readonly GrowableNativeBuffer<byte> _bytes = new();
        private int[] _slots = new int[MinSlots];  // index + 1; 0 is an empty slot
        private int[] _hashes = new int[MinSlots];
        private int _count;

        private DictionaryEncoder()
        {
            _offsets.Add(0);
        }

        /// <summary>
        /// The indices of a column's <paramref name="rows"/> values, given as <paramref name="offsets"/> into
        /// <paramref name="data"/>, and its distinct values as Arrow utf8 offsets and bytes. Every buffer is
        /// allocated with <see cref="Marshal.AllocHGlobal(nint)"/>; the caller frees them.
        /// </summary>
        internal static int* Encode(int* offsets, byte* data, long rows, out int* valueOffsets, out byte* valueBytes, out long valueCount, out long valueBytesLength)
        {
            using var encoder = new DictionaryEncoder();
            int* indices = (int*)Marshal.AllocHGlobal((nint)(Math.Max(1, rows) * sizeof(int)));
            try
            {
                for (long row = 0; row < rows; row++)
                {
                    indices[row] = encoder.IndexOf(new ReadOnlySpan<byte>(data + offsets[row], offsets[row + 1] - offsets[row]));
                }
            }
            catch
            {
                Marshal.FreeHGlobal((IntPtr)indices);
                throw;
            }
            valueCount = encoder._count;
            valueBytesLength = encoder._bytes.Count;
            valueOffsets = (int*)encoder._offsets.Detach();
            valueBytes = valueBytesLength == 0 ? (byte*)Marshal.AllocHGlobal(1) : (byte*)encoder._bytes.Detach();
            return indices;
        }

        private int IndexOf(ReadOnlySpan<byte> value)
        {
            var hasher = default(HashCode);
            hasher.AddBytes(value);
            int hash = hasher.ToHashCode();
            int mask = _slots.Length - 1;
            int slot = hash & mask;
            // The table is at most half full, so the probe always reaches an empty slot.
            while (_slots[slot] != 0)
            {
                int entry = _slots[slot];
                if (_hashes[slot] == hash && Value(entry - 1).SequenceEqual(value))
                {
                    return entry - 1;
                }
                slot = (slot + 1) & mask;
            }
            return Add(value, hash, slot);
        }

        private ReadOnlySpan<byte> Value(int index)
        {
            var offsets = _offsets.Span;
            return _bytes.Span[offsets[index]..offsets[index + 1]];
        }

        private int Add(ReadOnlySpan<byte> value, int hash, int slot)
        {
            int index = _count++;
            _bytes.AddRange(value);
            _offsets.Add(_bytes.Count);
            _slots[slot] = index + 1;
            _hashes[slot] = hash;
            // Half full at most, so a probe ends quickly at an empty slot.
            if (_count * 2 > _slots.Length)
            {
                Rehash();
            }
            return index;
        }

        private void Rehash()
        {
            int[] slots = new int[_slots.Length * 2];
            int[] hashes = new int[slots.Length];
            int mask = slots.Length - 1;
            for (int old = 0; old < _slots.Length; old++)
            {
                if (_slots[old] == 0)
                {
                    continue;
                }
                int slot = _hashes[old] & mask;
                while (slots[slot] != 0)
                {
                    slot = (slot + 1) & mask;
                }
                slots[slot] = _slots[old];
                hashes[slot] = _hashes[old];
            }
            _slots = slots;
            _hashes = hashes;
        }

        public void Dispose()
        {
            _offsets.Dispose();
            _bytes.Dispose();
        }
    }
}
