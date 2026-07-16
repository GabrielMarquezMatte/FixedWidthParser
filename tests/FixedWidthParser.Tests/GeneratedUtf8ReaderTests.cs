using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Readers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Covers the source-generated UTF-8 streaming path via <see cref="FixedWidthUtf8"/> (sync and
    /// async): raw-byte reading with no transcode, line-ending handling, BOM skipping, buffer growth,
    /// error reporting, leaveOpen behavior, struct-enumerator allocation freedom and cancellation.
    /// Mirrors <see cref="ByteReaderStreamTests"/> but exercises the generated <c>TryParse</c> strategy
    /// instead of the reflection parser.
    /// </summary>
    public class GeneratedUtf8ReaderTests
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
            using var stream = Utf8(TwoPeople);

            var people = FixedWidthUtf8.Read<GenPersonModel>(stream, formatProvider: Inv).ToList();

            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal(30, people[0].Age);
            Assert.Equal(60000.00, people[0].Salary, 2);
            Assert.Equal("Jane", people[1].Name);
            Assert.Equal(55000.00, people[1].Salary, 2);
        }

        [Fact]
        public void Read_StructEnumerator_IteratesManually()
        {
            // Exercises the public struct core returned by GeneratedUtf8FixedWidthRecordEnumerable directly
            // (GetEnumerator / MoveNext / Current / Dispose) rather than through foreach.
            using var stream = Utf8(TwoPeople);
            var enumerable = FixedWidthUtf8.Read<GenPersonModel>(stream, formatProvider: Inv);

            using var enumerator = enumerable.GetEnumerator();
            var names = new List<string>();
            while (enumerator.MoveNext())
            {
                names.Add(enumerator.Current.Name);
            }

            Assert.Equal(["John Doe", "Jane"], names);
        }

        [Theory]
        [InlineData("ABC\nDEF\n")]    // LF + trailing newline
        [InlineData("ABC\r\nDEF\r\n")] // CRLF
        [InlineData("ABC\nDEF")]       // no trailing newline on the last line
        public void Read_HandlesLineEndingsAndTrailingNewline(string text)
        {
            using var stream = Utf8(text);

            var codes = FixedWidthUtf8.Read<GenCodeModel>(stream).Select(m => m.Code).ToList();

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public void Read_SkipsEmptyLines()
        {
            using var stream = Utf8("ABC\n\n\nDEF\n");

            var codes = FixedWidthUtf8.Read<GenCodeModel>(stream).Select(m => m.Code).ToList();

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public void Read_SkipsLeadingUtf8Bom()
        {
            byte[] bytes = [0xEF, 0xBB, 0xBF, .. "ABC\nDEF\n"u8];
            using var stream = new MemoryStream(bytes);

            var codes = FixedWidthUtf8.Read<GenCodeModel>(stream).Select(m => m.Code).ToList();

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public void Read_LineLongerThanBuffer_StillParses()
        {
            // A tiny bufferSize forces buffer compaction and growth (25-byte lines).
            using var stream = Utf8(TwoPeople);

            var people = FixedWidthUtf8.Read<GenPersonModel>(stream, formatProvider: Inv, bufferSize: 4).ToList();

            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal("Jane", people[1].Name);
            Assert.Equal(55000.00, people[1].Salary, 2);
        }

        [Fact]
        public void Read_InvalidLine_ThrowsWithLineNumber()
        {
            using var stream = Utf8("John Doe  30   60000.00  \nJane      XX   55000.00  ");

            var ex = Assert.Throws<FormatException>(() => FixedWidthUtf8.Read<GenPersonModel>(stream, formatProvider: Inv).ToList());

            Assert.Contains("Line 2", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Read_LeaveOpenTrue_KeepsStreamOpen()
        {
            using var stream = Utf8("ABC\nDEF\n");

            _ = FixedWidthUtf8.Read<GenCodeModel>(stream, leaveOpen: true).ToList();

            Assert.True(stream.CanRead);
        }

        [Fact]
        public void Read_LeaveOpenFalse_DisposesStream()
        {
            var stream = Utf8("ABC\nDEF\n");

            _ = FixedWidthUtf8.Read<GenCodeModel>(stream).ToList(); // default leaveOpen: false

            Assert.False(stream.CanRead);
        }

        [Fact]
        public void Read_NullStream_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => FixedWidthUtf8.Read<GenCodeModel>(null!));
        }

        [Fact]
        public void GetEnumerator_IsStruct_ForAllocationFreeForeach()
        {
            Assert.True(typeof(Utf8RecordEnumeratorCore<GenCodeModel, GeneratedUtf8LineParser<GenCodeModel>>).IsValueType);

            using var stream = Utf8("ABC\nDEF\n");
            int count = 0;
            foreach (var _ in FixedWidthUtf8.Read<GenCodeModel>(stream))
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

                var first = FixedWidthUtf8.ReadFile<GenCodeModel>(path).Select(m => m.Code).ToList();
                var second = FixedWidthUtf8.ReadFile<GenCodeModel>(path).Select(m => m.Code).ToList();

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
            await using var stream = Utf8(TwoPeople);

            var people = await FixedWidthUtf8.ReadAsync<GenPersonModel>(stream, formatProvider: Inv).ToListAsync().ConfigureAwait(true);

            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal(30, people[0].Age);
            Assert.Equal("Jane", people[1].Name);
            Assert.Equal(55000.00, people[1].Salary, 2);
        }

        [Fact]
        public async Task ReadAsync_LineLongerThanBuffer_StillParses()
        {
            await using var stream = Utf8(TwoPeople);

            var people = await FixedWidthUtf8.ReadAsync<GenPersonModel>(stream, formatProvider: Inv, bufferSize: 4).ToListAsync().ConfigureAwait(true);

            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal(55000.00, people[1].Salary, 2);
        }

        [Fact]
        public async Task ReadAsync_SkipsLeadingUtf8Bom()
        {
            byte[] bytes = [0xEF, 0xBB, 0xBF, .. "ABC\nDEF\n"u8];
            await using var stream = new MemoryStream(bytes);

            var codes = await FixedWidthUtf8.ReadAsync<GenCodeModel>(stream).Select(m => m.Code).ToListAsync().ConfigureAwait(true);

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public async Task ReadAsync_InvalidLine_ThrowsWithLineNumber()
        {
            await using var stream = Utf8("John Doe  30   60000.00  \nJane      XX   55000.00  ");

            var ex = await Assert.ThrowsAsync<FormatException>(async () =>
            {
                await foreach (var _ in FixedWidthUtf8.ReadAsync<GenPersonModel>(stream, formatProvider: Inv).ConfigureAwait(false)) { }
            }).ConfigureAwait(true);

            Assert.Contains("Line 2", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ReadAsync_CancelledToken_Throws()
        {
            await using var stream = Utf8("ABC\nDEF\n");
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync().ConfigureAwait(true);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in FixedWidthUtf8.ReadAsync<GenCodeModel>(stream).WithCancellation(cts.Token).ConfigureAwait(false)) { }
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task ReadAsync_LeaveOpenTrue_KeepsStreamOpen()
        {
            await using var stream = Utf8("ABC\nDEF\n");

            _ = await FixedWidthUtf8.ReadAsync<GenCodeModel>(stream, leaveOpen: true).ToListAsync().ConfigureAwait(true);

            Assert.True(stream.CanRead);
        }

        [Fact]
        public async Task ReadAsync_LeaveOpenFalse_DisposesStream()
        {
            var stream = Utf8("ABC\nDEF\n");

            _ = await FixedWidthUtf8.ReadAsync<GenCodeModel>(stream).ToListAsync().ConfigureAwait(true);

            Assert.False(stream.CanRead);
        }

        [Fact]
        public void ReadAsync_NullStream_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => FixedWidthUtf8.ReadAsync<GenCodeModel>((Stream)null!));
        }

        [Fact]
        public async Task ReadFileAsync_MatchesSyncReadFile()
        {
            string path = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(path, "ABC\nDEF\nGHI\n").ConfigureAwait(true);

                var asyncCodes = await FixedWidthUtf8.ReadFileAsync<GenCodeModel>(path).Select(m => m.Code).ToListAsync().ConfigureAwait(true);
                var syncCodes = FixedWidthUtf8.ReadFile<GenCodeModel>(path).Select(m => m.Code).ToList();

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
            using var stream = Utf8("ABC\nABC\n");

            var codes = FixedWidthUtf8.Read<GenCodeModel>(stream, stringPool: pool).ToList();

            Assert.Equal("ABC", codes[0].Code);
            Assert.Same(codes[0].Code, codes[1].Code);
        }

        // ----------------------------- generated/reflection parity -----------------------------

        [Fact]
        public void GeneratedReader_MatchesReflectionByteReader_ForAsciiStream()
        {
            var reflection = new FixedWidthByteReader<PersonModel>(Inv);

            using var reflectionStream = Utf8(TwoPeople);
            var fromReflection = reflection.Read(reflectionStream).ToList();
            using var generatedStream = Utf8(TwoPeople);
            var fromGenerated = FixedWidthUtf8.Read<GenPersonModel>(generatedStream, formatProvider: Inv).ToList();

            Assert.Equal(fromReflection.Count, fromGenerated.Count);
            for (int i = 0; i < fromReflection.Count; i++)
            {
                Assert.Equal(fromReflection[i].Name, fromGenerated[i].Name);
                Assert.Equal(fromReflection[i].Age, fromGenerated[i].Age);
                Assert.Equal(fromReflection[i].Salary, fromGenerated[i].Salary, 2);
            }
        }
    }
}
