using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Readers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Covers the streaming Stream/file overloads of <see cref="FixedWidthByteReader{TModel}"/> (sync
    /// and async): raw-byte reading with no transcode, line-ending handling, BOM skipping, buffer
    /// growth, error reporting, leaveOpen behavior, and byte/char parity.
    /// </summary>
    public class ByteReaderStreamTests
    {
        private const string TwoPeople =
            "John Doe  30   60000.00  \n" +
            "Jane      28   55000.00  ";

        private static MemoryStream Utf8(string text)
        {
            return new(Encoding.UTF8.GetBytes(text));
        }

        // ----------------------------- Synchronous -----------------------------

        [Fact]
        public void Read_Stream_ParsesAll()
        {
            var reader = new FixedWidthByteReader<PersonModel>(Inv);
            using var stream = Utf8(TwoPeople);

            var people = reader.Read(stream).ToList();

            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal(30, people[0].Age);
            Assert.Equal(60000.00, people[0].Salary, 2);
            Assert.Equal("Jane", people[1].Name);
            Assert.Equal(55000.00, people[1].Salary, 2);
        }

        [Theory]
        [InlineData("ABC\nDEF\n")]    // LF + trailing newline
        [InlineData("ABC\r\nDEF\r\n")] // CRLF
        [InlineData("ABC\nDEF")]       // no trailing newline on the last line
        public void Read_HandlesLineEndingsAndTrailingNewline(string text)
        {
            var reader = new FixedWidthByteReader<CodeModel>();
            using var stream = Utf8(text);

            var codes = reader.Read(stream).Select(m => m.Code).ToList();

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public void Read_SkipsEmptyLines()
        {
            var reader = new FixedWidthByteReader<CodeModel>();
            using var stream = Utf8("ABC\n\n\nDEF\n");

            var codes = reader.Read(stream).Select(m => m.Code).ToList();

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public void Read_SkipsLeadingUtf8Bom()
        {
            var reader = new FixedWidthByteReader<CodeModel>();
            byte[] bytes = [0xEF, 0xBB, 0xBF, .. "ABC\nDEF\n"u8];
            using var stream = new MemoryStream(bytes);

            var codes = reader.Read(stream).Select(m => m.Code).ToList();

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public void Read_LineLongerThanBuffer_StillParses()
        {
            // A tiny bufferSize forces buffer compaction and growth (25-byte lines).
            var reader = new FixedWidthByteReader<PersonModel>(Inv, bufferSize: 4);
            using var stream = Utf8(TwoPeople);

            var people = reader.Read(stream).ToList();

            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal("Jane", people[1].Name);
            Assert.Equal(55000.00, people[1].Salary, 2);
        }

        [Fact]
        public void Read_InvalidLine_ThrowsWithLineNumber()
        {
            var reader = new FixedWidthByteReader<PersonModel>(Inv);
            using var stream = Utf8("John Doe  30   60000.00  \nJane      XX   55000.00  ");

            var ex = Assert.Throws<FormatException>(() => reader.Read(stream).ToList());

            Assert.Contains("Line 2", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Read_LeaveOpenTrue_KeepsStreamOpen()
        {
            var reader = new FixedWidthByteReader<CodeModel>();
            using var stream = Utf8("ABC\nDEF\n");

            _ = reader.Read(stream, leaveOpen: true).ToList();

            Assert.True(stream.CanRead);
        }

        [Fact]
        public void Read_LeaveOpenFalse_DisposesStream()
        {
            var reader = new FixedWidthByteReader<CodeModel>();
            var stream = Utf8("ABC\nDEF\n");

            _ = reader.Read(stream).ToList(); // default leaveOpen: false

            Assert.False(stream.CanRead);
        }

        [Fact]
        public void Read_NullStream_Throws()
        {
            var reader = new FixedWidthByteReader<CodeModel>();

            Assert.Throws<ArgumentNullException>(() => reader.Read(null!));
        }

        [Fact]
        public void GetEnumerator_IsStruct_ForAllocationFreeForeach()
        {
            Assert.True(typeof(RecordEnumeratorCore<byte, Utf8LineFormat, CodeModel, ReflectionUtf8LineParser<CodeModel>, StreamSource>).IsValueType);

            var reader = new FixedWidthByteReader<CodeModel>();
            using var stream = Utf8("ABC\nDEF\n");
            int count = 0;
            foreach (var _ in reader.Read(stream))
            {
                count++;
            }

            Assert.Equal(2, count);
        }

        [Fact]
        public void ReadFile_IsReEnumerable()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "ABC\nDEF\n");
                var reader = new FixedWidthByteReader<CodeModel>();

                var first = reader.ReadFile(path).Select(m => m.Code).ToList();
                var second = reader.ReadFile(path).Select(m => m.Code).ToList();

                Assert.Equal(["ABC", "DEF"], first);
                Assert.Equal(first, second);
            }
            finally
            {
                File.Delete(path);
            }
        }

        // ----------------------------- Asynchronous -----------------------------

        [Fact]
        public async Task ReadAsync_Stream_ParsesAll()
        {
            var reader = new FixedWidthByteReader<PersonModel>(Inv);
            await using var stream = Utf8(TwoPeople);

            var people = await reader.ReadAsync(stream).ToListAsync().ConfigureAwait(true);

            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal(30, people[0].Age);
            Assert.Equal("Jane", people[1].Name);
            Assert.Equal(55000.00, people[1].Salary, 2);
        }

        [Fact]
        public async Task ReadAsync_LineLongerThanBuffer_StillParses()
        {
            var reader = new FixedWidthByteReader<PersonModel>(Inv, bufferSize: 4);
            await using var stream = Utf8(TwoPeople);

            var people = await reader.ReadAsync(stream).ToListAsync().ConfigureAwait(true);

            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal(55000.00, people[1].Salary, 2);
        }

        [Fact]
        public async Task ReadAsync_SkipsLeadingUtf8Bom()
        {
            var reader = new FixedWidthByteReader<CodeModel>();
            byte[] bytes = [0xEF, 0xBB, 0xBF, .. "ABC\nDEF\n"u8];
            await using var stream = new MemoryStream(bytes);

            var codes = await reader.ReadAsync(stream).Select(m => m.Code).ToListAsync().ConfigureAwait(true);

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public async Task ReadAsync_InvalidLine_ThrowsWithLineNumber()
        {
            var reader = new FixedWidthByteReader<PersonModel>(Inv);
            await using var stream = Utf8("John Doe  30   60000.00  \nJane      XX   55000.00  ");

            var ex = await Assert.ThrowsAsync<FormatException>(async () =>
            {
                await foreach (var _ in reader.ReadAsync(stream).ConfigureAwait(false)) { }
            }).ConfigureAwait(true);

            Assert.Contains("Line 2", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ReadAsync_CancelledToken_Throws()
        {
            var reader = new FixedWidthByteReader<CodeModel>();
            await using var stream = Utf8("ABC\nDEF\n");
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync().ConfigureAwait(true);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in reader.ReadAsync(stream).WithCancellation(cts.Token).ConfigureAwait(false)) { }
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task ReadAsync_LeaveOpenTrue_KeepsStreamOpen()
        {
            var reader = new FixedWidthByteReader<CodeModel>();
            await using var stream = Utf8("ABC\nDEF\n");

            _ = await reader.ReadAsync(stream, leaveOpen: true).ToListAsync().ConfigureAwait(true);

            Assert.True(stream.CanRead);
        }

        [Fact]
        public async Task ReadAsync_LeaveOpenFalse_DisposesStream()
        {
            var reader = new FixedWidthByteReader<CodeModel>();
            var stream = Utf8("ABC\nDEF\n");

            _ = await reader.ReadAsync(stream).ToListAsync().ConfigureAwait(true);

            Assert.False(stream.CanRead);
        }

        [Fact]
        public void ReadAsync_NullStream_Throws()
        {
            var reader = new FixedWidthByteReader<CodeModel>();

            Assert.Throws<ArgumentNullException>(() => reader.ReadAsync((Stream)null!));
        }

        [Fact]
        public async Task ReadFileAsync_MatchesSyncReadFile()
        {
            string path = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(path, "ABC\nDEF\nGHI\n").ConfigureAwait(true);
                var reader = new FixedWidthByteReader<CodeModel>();

                var asyncCodes = await reader.ReadFileAsync(path).Select(m => m.Code).ToListAsync().ConfigureAwait(true);
                var syncCodes = reader.ReadFile(path).Select(m => m.Code).ToList();

                Assert.Equal(["ABC", "DEF", "GHI"], asyncCodes);
                Assert.Equal(syncCodes, asyncCodes);
            }
            finally
            {
                File.Delete(path);
            }
        }

        // ----------------------------- StringPool -----------------------------

        [Fact]
        public void Read_Stream_WithStringPool_InternsRepeatedValues()
        {
            var pool = new StringPool();
            var reader = new FixedWidthByteReader<CodeModel>(stringPool: pool);
            using var stream = Utf8("ABC\nABC\n");

            var codes = reader.Read(stream).ToList();

            Assert.Equal("ABC", codes[0].Code);
            Assert.Same(codes[0].Code, codes[1].Code);
        }

        [Fact]
        public async Task ReadAsync_Stream_WithStringPool_InternsRepeatedValues()
        {
            var pool = new StringPool();
            var reader = new FixedWidthByteReader<CodeModel>(stringPool: pool);
            await using var stream = Utf8("ABC\nABC\n");

            var codes = await reader.ReadAsync(stream).ToListAsync().ConfigureAwait(true);

            Assert.Equal("ABC", codes[0].Code);
            Assert.Same(codes[0].Code, codes[1].Code);
        }

        // ----------------------------- char/byte parity -----------------------------

        [Fact]
        public void ByteReader_MatchesCharReader_ForAsciiStream()
        {
            var charReader = new FixedWidthReader<PersonModel>(Inv);
            var byteReader = new FixedWidthByteReader<PersonModel>(Inv);

            var fromChars = charReader.Read(new StringReader(TwoPeople)).ToList();
            using var stream = Utf8(TwoPeople);
            var fromBytes = byteReader.Read(stream).ToList();

            Assert.Equal(fromChars.Count, fromBytes.Count);
            for (int i = 0; i < fromChars.Count; i++)
            {
                Assert.Equal(fromChars[i].Name, fromBytes[i].Name);
                Assert.Equal(fromChars[i].Age, fromBytes[i].Age);
                Assert.Equal(fromChars[i].Salary, fromBytes[i].Salary, 2);
            }
        }
    }
}
