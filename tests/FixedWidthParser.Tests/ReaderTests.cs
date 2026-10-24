using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Readers;
using FixedWidthParser.Writers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// <see cref="FixedWidthReader{TModel}"/> over streams and files, sync and async: line endings, BOM,
    /// buffer growth, error reporting, leaveOpen and string pooling.
    /// </summary>
    public class ReaderTests
    {
        // Lines in the PersonModel layout (Name[0,10) Age[10,5) Salary[15,10)), 25 bytes each.
        private const string TwoPeople =
            "John Doe  30   60000.00  \n" +
            "Jane      28   55000.00  ";

        [Fact]
        public void Read_MultipleLines_ParsesAll()
        {
            var people = new FixedWidthReader<PersonModel>().Read(StreamOf(TwoPeople)).ToList();

            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal(30, people[0].Age);
            Assert.Equal(60000.00, people[0].Salary, 2);
            Assert.Equal("Jane", people[1].Name);
            Assert.Equal(55000.00, people[1].Salary, 2);
        }

        [Theory]
        [InlineData("ABC\nDEF\n")]
        [InlineData("ABC\r\nDEF\r\n")]
        [InlineData("ABC\nDEF")]
        public void Read_HandlesLineEndingsAndTrailingNewline(string text)
        {
            var codes = new FixedWidthReader<CodeModel>().Read(StreamOf(text)).Select(m => m.Code).ToList();

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public void Read_SkipsEmptyLines()
        {
            var codes = new FixedWidthReader<CodeModel>().Read(StreamOf("ABC\n\n\nDEF\n")).Select(m => m.Code).ToList();

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public void Read_SkipsLeadingUtf8Bom()
        {
            byte[] bytes = [0xEF, 0xBB, 0xBF, .. "ABC\nDEF\n"u8];

            var codes = new FixedWidthReader<CodeModel>().Read(new MemoryStream(bytes)).Select(m => m.Code).ToList();

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public void Read_LineLongerThanBuffer_StillParses()
        {
            // A tiny buffer forces compaction and growth (25-byte lines).
            var reader = new FixedWidthReader<PersonModel>(new FixedWidthReaderOptions { BufferSize = 4 });

            var people = reader.Read(StreamOf(TwoPeople)).ToList();

            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal(55000.00, people[1].Salary, 2);
        }

        [Fact]
        public void Read_InvalidLine_ThrowsWithLineNumber()
        {
            var reader = new FixedWidthReader<PersonModel>();

            var ex = Assert.Throws<FixedWidthParseException>(
                () => reader.Read(StreamOf("John Doe  30   60000.00  \nJane      XX   55000.00  ")).ToList());

            Assert.Equal(2, ex.LineNumber);
            Assert.Contains("Line 2", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Read_LeaveOpenTrue_KeepsStreamOpen()
        {
            using var stream = StreamOf("ABC\nDEF\n");

            _ = new FixedWidthReader<CodeModel>().Read(stream, leaveOpen: true).ToList();

            Assert.True(stream.CanRead);
        }

        [Fact]
        public void Read_LeaveOpenFalse_DisposesStream()
        {
            var stream = StreamOf("ABC\nDEF\n");

            _ = new FixedWidthReader<CodeModel>().Read(stream).ToList();

            Assert.False(stream.CanRead);
        }

        [Fact]
        public void Read_NullStream_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new FixedWidthReader<CodeModel>().Read(null!));
        }

        [Fact]
        public void Read_WithStringPool_InternsRepeatedValues()
        {
            var reader = new FixedWidthReader<CodeModel>(new FixedWidthReaderOptions { StringPool = new StringPool() });

            var codes = reader.Read(StreamOf("ABC\nABC\n")).ToList();

            Assert.Equal("ABC", codes[0].Code);
            Assert.Same(codes[0].Code, codes[1].Code);
        }

        [Fact]
        public void GetEnumerator_IsStruct_ForAllocationFreeForeach()
        {
            Assert.True(typeof(RecordEnumerator<CodeModel, LayoutLineParser<CodeModel>>).IsValueType);

            int count = 0;
            foreach (var _ in new FixedWidthReader<CodeModel>().Read(StreamOf("ABC\nDEF\n")))
            {
                count++;
            }

            Assert.Equal(2, count);
        }

        [Fact]
        public void TryParse_UsesTheReaderOptions()
        {
            var reader = new FixedWidthReader<DecimalModel>(new FixedWidthReaderOptions { FormatProvider = System.Globalization.CultureInfo.GetCultureInfo("de-DE") });

            Assert.True(reader.TryParse("1234,56     "u8, out var model));
            Assert.Equal(1234.56m, model.Amount);
            Assert.False(reader.TryParse("short"u8, out _));
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

        [Fact]
        public void Writer_To_File_To_Reader_RoundTrip()
        {
            string path = Path.GetTempFileName();
            try
            {
                PersonModel[] models =
                [
                    new() { Name = "John Doe", Age = 30, Salary = 60000 },
                    new() { Name = "Jane", Age = 28, Salary = 55000 },
                ];
                using (var fs = File.Create(path))
                {
                    new FixedWidthWriter<PersonModel>().WriteMany(fs, models.AsSpan());
                }

                var parsed = new FixedWidthReader<PersonModel>().ReadFile(path).ToList();

                Assert.Equal(2, parsed.Count);
                Assert.Equal("John Doe", parsed[0].Name);
                Assert.Equal(60000, parsed[0].Salary, 2);
                Assert.Equal("Jane", parsed[1].Name);
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
            var people = await new FixedWidthReader<PersonModel>().ReadAsync(StreamOf(TwoPeople)).ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal("Jane", people[1].Name);
            Assert.Equal(55000.00, people[1].Salary, 2);
        }

        [Fact]
        public async Task ReadAsync_LineLongerThanBuffer_StillParses()
        {
            var reader = new FixedWidthReader<PersonModel>(new FixedWidthReaderOptions { BufferSize = 4 });

            var people = await reader.ReadAsync(StreamOf(TwoPeople)).ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, people.Count);
            Assert.Equal(55000.00, people[1].Salary, 2);
        }

        [Fact]
        public async Task ReadAsync_SkipsLeadingUtf8Bom()
        {
            byte[] bytes = [0xEF, 0xBB, 0xBF, .. "ABC\nDEF\n"u8];

            var codes = await new FixedWidthReader<CodeModel>().ReadAsync(new MemoryStream(bytes)).Select(m => m.Code).ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public async Task ReadAsync_InvalidLine_ThrowsWithLineNumber()
        {
            var reader = new FixedWidthReader<PersonModel>();

            var ex = await Assert.ThrowsAsync<FixedWidthParseException>(async () =>
            {
                await foreach (var _ in reader.ReadAsync(StreamOf("John Doe  30   60000.00  \nJane      XX   55000.00  ")))
                {
                }
            });

            Assert.Equal(2, ex.LineNumber);
        }

        [Fact]
        public async Task ReadAsync_CancelledToken_Throws()
        {
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in new FixedWidthReader<CodeModel>().ReadAsync(StreamOf("ABC\nDEF\n")).WithCancellation(cts.Token))
                {
                }
            });
        }

        [Fact]
        public async Task ReadAsync_LeaveOpen_ControlsDisposal()
        {
            var reader = new FixedWidthReader<CodeModel>();
            using var kept = StreamOf("ABC\n");
            var owned = StreamOf("ABC\n");

            _ = await reader.ReadAsync(kept, leaveOpen: true).ToListAsync(TestContext.Current.CancellationToken);
            _ = await reader.ReadAsync(owned).ToListAsync(TestContext.Current.CancellationToken);

            Assert.True(kept.CanRead);
            Assert.False(owned.CanRead);
        }

        [Fact]
        public void ReadAsync_NullStream_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new FixedWidthReader<CodeModel>().ReadAsync((Stream)null!));
        }

        [Fact]
        public async Task ReadFileAsync_MatchesSyncReadFile()
        {
            string path = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(path, "ABC\nDEF\nGHI\n", TestContext.Current.CancellationToken);
                var reader = new FixedWidthReader<CodeModel>();

                var asyncCodes = await reader.ReadFileAsync(path).Select(m => m.Code).ToListAsync(TestContext.Current.CancellationToken);
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
        public async Task ReadAsync_WithStringPool_InternsRepeatedValues()
        {
            var reader = new FixedWidthReader<CodeModel>(new FixedWidthReaderOptions { StringPool = new StringPool() });

            var codes = await reader.ReadAsync(StreamOf("ABC\nABC\n")).ToListAsync(TestContext.Current.CancellationToken);

            Assert.Same(codes[0].Code, codes[1].Code);
        }
    }
}
