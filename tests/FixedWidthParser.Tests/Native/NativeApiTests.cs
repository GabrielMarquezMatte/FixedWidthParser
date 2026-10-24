using System.Text;
using FixedWidthParser.Native;

namespace FixedWidthParser.Tests.Native
{
    public sealed class NativeApiTests
    {
        private static string LastError()
        {
            Span<byte> buffer = stackalloc byte[512];
            Assert.Equal(NativeStatus.Ok, NativeApi.LastError(buffer, out int length));
            return Encoding.UTF8.GetString(buffer[..length]);
        }

        [Fact]
        public void LastError_IsPerThread()
        {
            NativeApi.SetLastError("on this thread");

            string? other = null;
            var thread = new Thread(() => other = LastError());
            thread.Start();
            thread.Join();

            Assert.Equal("", other);
            Assert.Equal("on this thread", LastError());
        }

        [Fact]
        public void LastError_BufferTooSmall_ReportsLength()
        {
            NativeApi.SetLastError("twelve bytes");

            Assert.Equal(NativeStatus.BufferTooSmall, NativeApi.LastError(new byte[4], out int length));
            Assert.Equal(12, length);
        }

        [Fact]
        public void ParseError_CarriesLineAndColumn()
        {
            var exception = new FixedWidthParseException("bad", 7, "Age", 10, 5, "XX");

            NativeApi.SetParseError(exception, column: 2);

            NativeApi.LastParseError(out long line, out int column);
            Assert.Equal(7, line);
            Assert.Equal(2, column);
            Assert.Equal("bad", LastError());
        }

        [Fact]
        public void SetLastError_ClearsTheParseError()
        {
            NativeApi.SetParseError(new FixedWidthParseException("bad", 7, null, -1, 0, null), column: -1);

            NativeApi.SetLastError("other");

            NativeApi.LastParseError(out long line, out int column);
            Assert.Equal(0, line);
            Assert.Equal(-1, column);
        }

        [Fact]
        public void HandleTable_IdIsNeverReissued()
        {
            nint first = NativeHandleTable.Register(new object());
            Assert.True(NativeHandleTable.TryUnregister<object>(first, out _));

            nint second = NativeHandleTable.Register(new object());

            Assert.NotEqual(first, second);
            Assert.Null(NativeHandleTable.Resolve<object>(first));
        }

        [Fact]
        public void HandleTable_ResolvesOnlyItsOwnKind()
        {
            nint id = NativeHandleTable.Register("a string");

            Assert.Null(NativeHandleTable.Resolve<List<int>>(id));
            Assert.Equal("a string", NativeHandleTable.Resolve<string>(id));
            Assert.True(NativeHandleTable.TryUnregister<string>(id, out _));
        }
    }
}
