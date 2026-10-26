using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using FixedWidthParser.Native.Reading;

namespace FixedWidthParser.Native.Writing
{
    /// <summary><c>fw_sink</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NativeSinkRaw
    {
        public int StructSize;
        public void* UserData;
        public delegate* unmanaged<void*, byte*, long, int> Write;
    }

    /// <summary>The caller's <c>fw_sink</c> as a write-only <see cref="Stream"/>: every byte handed to it, in order.</summary>
    internal sealed unsafe class CallbackWriteStream : Stream
    {
        private readonly delegate* unmanaged<void*, byte*, long, int> _write;
        private readonly void* _userData;

        private CallbackWriteStream(NativeSinkRaw raw)
        {
            _write = raw.Write;
            _userData = raw.UserData;
        }

        /// <summary>Wraps <paramref name="raw"/>, or explains why it cannot.</summary>
        internal static bool TryCreate(NativeSinkRaw* raw, [NotNullWhen(true)] out CallbackWriteStream? stream)
        {
            stream = null;
            if (raw is null || raw->StructSize != sizeof(NativeSinkRaw) || raw->Write is null)
            {
                NativeApi.SetLastError("fw_write_stream needs a non-NULL sink whose struct_size is sizeof(fw_sink) and whose write is set.");
                return false;
            }
            stream = new CallbackWriteStream(*raw);
            return true;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.IsEmpty)
            {
                return;
            }
            SourceErrors.Clear();
            int status;
            fixed (byte* source = buffer)
            {
                status = _write(_userData, source, buffer.Length);
            }
            if (status != 0)
            {
                throw SourceErrors.Failed("fw_sink.write", -1, buffer.Length);
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Write(buffer.AsSpan(offset, count));
        }

        public override void Flush()
        {
            // Every write has reached the sink when its callback returns.
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
    }
}
