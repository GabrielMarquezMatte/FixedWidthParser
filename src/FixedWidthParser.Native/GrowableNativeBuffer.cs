using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FixedWidthParser.Native
{
    /// <summary>
    /// One native block that doubles as it fills, so a finished column can be handed over as-is instead
    /// of copied. A <see cref="SafeHandle"/> owns the block, so a buffer dropped on an error path still
    /// frees it.
    /// </summary>
    internal sealed unsafe class GrowableNativeBuffer<T> : IDisposable where T : unmanaged
    {
        private const int InitialLength = 256;
#pragma warning disable SS066 // Disposed in Dispose, or detached by Detach.
        private Block? _block;
#pragma warning restore SS066
        private T* _data;
        private int _capacity;

        internal int Count { get; private set; }

        internal ref T this[int index] => ref _data[index];

        internal ReadOnlySpan<T> Span => new(_data, Count);

        /// <summary>Drops every element from <paramref name="count"/> on.</summary>
        internal void Truncate(int count)
        {
            Count = count;
        }

        /// <summary>Appends <paramref name="count"/> uninitialized elements and returns them to be filled.</summary>
        internal Span<T> Append(int count)
        {
            if (count > _capacity - Count)
            {
                Grow((long)Count + count);
            }
            var span = new Span<T>(_data + Count, count);
            Count += count;
            return span;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Add(T value)
        {
            if (Count == _capacity)
            {
                Grow(Count + 1L);
            }
            _data[Count++] = value;
        }

        internal void AddRange(ReadOnlySpan<T> values)
        {
            if (values.Length > _capacity - Count)
            {
                Grow((long)Count + values.Length);
            }
            values.CopyTo(new Span<T>(_data + Count, values.Length));
            Count += values.Length;
        }

        /// <summary>Hands the block to the caller, who frees it with <see cref="Marshal.FreeHGlobal"/>.</summary>
        internal IntPtr Detach()
        {
            IntPtr block = (IntPtr)_data;
            _block?.SetHandleAsInvalid();
            Reset();
            return block;
        }

        public void Dispose()
        {
            _block?.Dispose();
            Reset();
        }

        private void Reset()
        {
#pragma warning disable IDISP003 // Callers dispose or detach the block first.
            _block = null;
#pragma warning restore IDISP003
            _data = null;
            _capacity = 0;
            Count = 0;
        }

        /// <summary>Doubles, at least to <paramref name="minimum"/>, clamped to int.MaxValue elements; past that the buffer cannot grow.</summary>
        internal static int NextCapacity(int capacity, long minimum)
        {
            if (minimum > int.MaxValue)
            {
                throw new InvalidOperationException($"A column buffer cannot hold more than {int.MaxValue} elements.");
            }
            return (int)Math.Min(int.MaxValue, Math.Max(minimum, Math.Max(capacity * 2L, InitialLength)));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Grow(long minimum)
        {
            int capacity = NextCapacity(_capacity, minimum);
            nint bytes = checked((nint)capacity * sizeof(T));
            _block ??= new Block();
            _data = (T*)_block.Resize(bytes);
            _capacity = capacity;
        }

        private sealed class Block : SafeHandle
        {
            internal Block()
                : base(IntPtr.Zero, ownsHandle: true)
            {
            }

            public override bool IsInvalid => handle == IntPtr.Zero;

            internal IntPtr Resize(nint bytes)
            {
                SetHandle(handle == IntPtr.Zero ? Marshal.AllocHGlobal(bytes) : Marshal.ReAllocHGlobal(handle, bytes));
                return handle;
            }

            protected override bool ReleaseHandle()
            {
                Marshal.FreeHGlobal(handle);
                return true;
            }
        }
    }
}
