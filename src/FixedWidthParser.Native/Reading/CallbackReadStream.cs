using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace FixedWidthParser.Native.Reading
{
    /// <summary><c>fw_stream</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NativeStreamRaw
    {
        public int StructSize;
        public void* UserData;
        public delegate* unmanaged<void*, byte*, long, long> Read;
        public delegate* unmanaged<void*, void> Release;
    }

    /// <summary>
    /// The caller's <c>fw_stream</c> as a forward-only <see cref="Stream"/>: ExcelReader's <c>xl_stream</c>. Disposing it
    /// calls <c>release</c> once.
    /// </summary>
    internal sealed unsafe class CallbackReadStream : Stream
    {
        private readonly delegate* unmanaged<void*, byte*, long, long> _read;
        private readonly delegate* unmanaged<void*, void> _release;
        private readonly void* _userData;
        private int _released;

        private CallbackReadStream(NativeStreamRaw raw)
        {
            _read = raw.Read;
            _release = raw.Release;
            _userData = raw.UserData;
        }

        /// <summary>Wraps <paramref name="raw"/>, or explains why it cannot; <c>release</c> is not called then.</summary>
        internal static bool TryCreate(NativeStreamRaw* raw, string function, [NotNullWhen(true)] out CallbackReadStream? stream)
        {
            stream = null;
            if (raw is null || raw->StructSize != sizeof(NativeStreamRaw) || raw->Read is null)
            {
                NativeApi.SetLastError($"{function} needs a non-NULL stream whose struct_size is sizeof(fw_stream) and whose read is set.");
                return false;
            }
            stream = new CallbackReadStream(*raw);
            return true;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(Span<byte> buffer)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _released) != 0, this);
            if (buffer.IsEmpty)
            {
                return 0;
            }
            SourceErrors.Clear();
            long got;
            fixed (byte* destination = buffer)
            {
                got = _read(_userData, destination, buffer.Length);
            }
            if (got < 0 || got > buffer.Length)
            {
                throw SourceErrors.Failed("fw_stream.read", got, buffer.Length);
            }
            return (int)got;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public override void Flush()
        {
            // Nothing to flush: the stream only reads.
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _released, 1) == 0 && _release is not null)
            {
                _release(_userData);
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>The message a stream callback leaves through <c>fw_set_source_error</c> before returning -1.</summary>
    internal static class SourceErrors
    {
        [ThreadStatic]
        private static string? _pending;

        internal static void Set(string? message)
        {
            _pending = string.IsNullOrEmpty(message) ? null : message;
        }

        internal static void Clear()
        {
            _pending = null;
        }

        internal static IOException Failed(string where, long got, long requested)
        {
            string? message = _pending;
            _pending = null;
            return got == -1
                ? new IOException(message ?? $"{where} failed.")
                : new IOException($"{where} returned {got} for a request of {requested} bytes, which is not a valid result.");
        }
    }
}
