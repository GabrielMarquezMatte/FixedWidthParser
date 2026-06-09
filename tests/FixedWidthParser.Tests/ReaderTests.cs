using System.Globalization;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Readers;
using FixedWidthParser.Writers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    public class ReaderTests
    {
        // Lines in the PersonModel layout (Name[0,10) Age[10,5) Salary[15,10)), 25 chars each.
        private const string TwoPeople =
            "John Doe  30   60000.00  \n" +
            "Jane      28   55000.00  ";

        [Fact]
        public void Read_MultipleLines_ParsesAll()
        {
            var reader = new FixedWidthReader<PersonModel>(Inv);

            var people = reader.Read(new StringReader(TwoPeople)).ToList();

            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal(30, people[0].Age);
            Assert.Equal(60000.00, people[0].Salary, 2);
            Assert.Equal("Jane", people[1].Name);
            Assert.Equal(28, people[1].Age);
        }

        [Theory]
        [InlineData("ABC\nDEF\n")]   // LF + trailing newline
        [InlineData("ABC\r\nDEF\r\n")] // CRLF
        [InlineData("ABC\nDEF")]      // no trailing newline on the last line
        public void Read_HandlesLineEndingsAndTrailingNewline(string text)
        {
            var reader = new FixedWidthReader<CodeModel>();

            var codes = reader.Read(new StringReader(text)).Select(m => m.Code).ToList();

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public void Read_SkipsEmptyLines()
        {
            var reader = new FixedWidthReader<CodeModel>();

            var codes = reader.Read(new StringReader("ABC\n\n\nDEF\n")).Select(m => m.Code).ToList();

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public void Read_LineLongerThanBuffer_StillParses()
        {
            // A tiny bufferSize forces buffer compaction and growth (25-char lines).
            var reader = new FixedWidthReader<PersonModel>(Inv, bufferSize: 4);

            var people = reader.Read(new StringReader(TwoPeople)).ToList();

            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal("Jane", people[1].Name);
            Assert.Equal(55000.00, people[1].Salary, 2);
        }

        [Fact]
        public void Read_InvalidLine_ThrowsWithLineNumber()
        {
            var reader = new FixedWidthReader<PersonModel>(Inv);
            var text = "John Doe  30   60000.00  \nJane      XX   55000.00  "; // invalid Age on line 2

            var ex = Assert.Throws<FormatException>(
                () => reader.Read(new StringReader(text)).ToList());

            Assert.Contains("Line 2", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Read_WithStringPool_InternsRepeatedValues()
        {
            var pool = new StringPool();
            var reader = new FixedWidthReader<CodeModel>(stringPool: pool);

            var codes = reader.Read(new StringReader("ABC\nABC\n")).ToList();

            Assert.Equal("ABC", codes[0].Code);
            Assert.Same(codes[0].Code, codes[1].Code);
        }

        [Fact]
        public void GetEnumerator_IsStruct_ForAllocationFreeForeach()
        {
            Assert.True(typeof(FixedWidthRecordEnumerable<CodeModel>.Enumerator).IsValueType);

            var reader = new FixedWidthReader<CodeModel>();
            int count = 0;
            foreach (var _ in reader.Read(new StringReader("ABC\nDEF\n"))) count++;

            Assert.Equal(2, count);
        }

        [Fact]
        public void ReadFile_IsReEnumerable()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "ABC\nDEF\n");
                var reader = new FixedWidthReader<CodeModel>();

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

        // ----------------------- Asynchronous reading -----------------------

        [Fact]
        public async Task ReadAsync_MultipleLines_ParsesAll()
        {
            var reader = new FixedWidthReader<PersonModel>(Inv);
            var people = await reader.ReadAsync(new StringReader(TwoPeople)).ToListAsync().ConfigureAwait(true);
            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal(30, people[0].Age);
            Assert.Equal("Jane", people[1].Name);
            Assert.Equal(55000.00, people[1].Salary, 2);
        }

        [Fact]
        public async Task ReadAsync_LineLongerThanBuffer_StillParses()
        {
            var reader = new FixedWidthReader<PersonModel>(Inv, bufferSize: 4);
            var people = await reader.ReadAsync(new StringReader(TwoPeople)).ToListAsync().ConfigureAwait(true);
            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal(55000.00, people[1].Salary, 2);
        }

        [Fact]
        public async Task ReadAsync_InvalidLine_ThrowsWithLineNumber()
        {
            var reader = new FixedWidthReader<PersonModel>(Inv);
            var text = "John Doe  30   60000.00  \nJane      XX   55000.00  ";

            var ex = await Assert.ThrowsAsync<FormatException>(async () =>
            {
                await foreach (var _ in reader.ReadAsync(new StringReader(text)).ConfigureAwait(false)) { }
            }).ConfigureAwait(true);

            Assert.Contains("Line 2", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ReadAsync_CancelledToken_Throws()
        {
            var reader = new FixedWidthReader<CodeModel>();
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync().ConfigureAwait(true);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in reader.ReadAsync(new StringReader("ABC\nDEF\n")).WithCancellation(cts.Token).ConfigureAwait(false)) { }
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task ReadFileAsync_MatchesSyncReadFile()
        {
            string path = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(path, "ABC\nDEF\nGHI\n").ConfigureAwait(true);
                var reader = new FixedWidthReader<CodeModel>();
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

        [Fact]
        public void Writer_To_File_To_Reader_RoundTrip()
        {
            string path = Path.GetTempFileName();
            try
            {
                var models = new[]
                {
                    new PersonModel { Name = "John Doe", Age = 30, Salary = 60000 },
                    new PersonModel { Name = "Jane",     Age = 28, Salary = 55000 },
                };
                var writer = new FixedWidthWriter<PersonModel>();
                using (var fs = File.Create(path))
                {
                    writer.WriteMany(fs, models.AsSpan(), Inv);
                }

                var reader = new FixedWidthReader<PersonModel>(Inv);
                var parsed = reader.ReadFile(path).ToList();

                Assert.Equal(2, parsed.Count);
                Assert.Equal("John Doe", parsed[0].Name);
                Assert.Equal(30, parsed[0].Age);
                Assert.Equal(60000, parsed[0].Salary, 2);
                Assert.Equal("Jane", parsed[1].Name);
                Assert.Equal(55000, parsed[1].Salary, 2);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
