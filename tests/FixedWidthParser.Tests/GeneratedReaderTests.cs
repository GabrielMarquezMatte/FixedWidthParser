using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Readers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// The <see cref="FixedWidth"/> read methods for generated models, sync and async, and their
    /// parity with reading through <see cref="FixedWidthLayout.FromAttributes{TModel}"/>.
    /// </summary>
    public class GeneratedReaderTests
    {
        private const string TwoPeople =
            "John Doe  30   60000.00  \n" +
            "Jane      28   55000.00  ";

        [Fact]
        public void Read_Stream_ParsesAll()
        {
            var people = FixedWidth.Read<GenPersonModel>(StreamOf(TwoPeople)).ToList();

            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal(30, people[0].Age);
            Assert.Equal("Jane", people[1].Name);
            Assert.Equal(55000.00, people[1].Salary, 2);
        }

        [Fact]
        public void Read_StructEnumerator_IteratesManually()
        {
            using var enumerator = FixedWidth.Read<GenPersonModel>(StreamOf(TwoPeople)).GetEnumerator();
            var names = new List<string>();
            while (enumerator.MoveNext())
            {
                names.Add(enumerator.Current.Name);
            }

            Assert.Equal(["John Doe", "Jane"], names);
        }

        [Theory]
        [InlineData("ABC\nDEF\n")]
        [InlineData("ABC\r\nDEF\r\n")]
        [InlineData("ABC\nDEF")]
        public void Read_HandlesLineEndingsAndTrailingNewline(string text)
        {
            var codes = FixedWidth.Read<GenCodeModel>(StreamOf(text)).Select(m => m.Code).ToList();

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public void Read_SkipsEmptyLinesAndBom()
        {
            byte[] bytes = [0xEF, 0xBB, 0xBF, .. "ABC\n\n\nDEF\n"u8];

            var codes = FixedWidth.Read<GenCodeModel>(new MemoryStream(bytes)).Select(m => m.Code).ToList();

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public void Read_Latin1_DecodesStringsAndKeepsBytePositions()
        {
            // 'Á' and 'é' are one byte each in Latin-1, so the 3-byte column holds three characters.
            var options = new FixedWidthReaderOptions { Encoding = Encoding.Latin1 };

            var codes = FixedWidth.Read<GenCodeModel>(new MemoryStream(Encoding.Latin1.GetBytes("Áb \nCDé\n")), options: options)
                .Select(m => m.Code)
                .ToList();

            Assert.Equal(["Áb", "CDé"], codes);
        }

        [Fact]
        public void Read_LineLongerThanBuffer_MatchesReflectionReader()
        {
            var options = new FixedWidthReaderOptions { BufferSize = 4 };

            var expected = FixedWidth.Read(StreamOf(TwoPeople), FixedWidthLayout.FromAttributes<PersonModel>(), options: options).ToList();
            var actual = FixedWidth.Read<GenPersonModel>(StreamOf(TwoPeople), options: options).ToList();

            Assert.Equal(expected[0].Name, actual[0].Name);
            Assert.Equal(expected[1].Name, actual[1].Name);
            Assert.Equal(expected[1].Salary, actual[1].Salary, 2);
        }

        [Fact]
        public void Read_InvalidLine_ThrowsSameErrorAsReflectionReader()
        {
            const string text = "John Doe  30   60000.00  \nJane      XX   55000.00  ";

            var expected = Assert.Throws<FixedWidthParseException>(() => FixedWidth.Read(StreamOf(text), FixedWidthLayout.FromAttributes<PersonModel>()).ToList());
            var actual = Assert.Throws<FixedWidthParseException>(() => FixedWidth.Read<GenPersonModel>(StreamOf(text)).ToList());

            Assert.Equal(expected.LineNumber, actual.LineNumber);
            Assert.Equal(expected.ColumnName, actual.ColumnName);
            Assert.Equal(expected.RawValue, actual.RawValue);
        }

        [Fact]
        public void Read_LeaveOpen_ControlsDisposal()
        {
            using var kept = StreamOf("ABC\n");
            var owned = StreamOf("ABC\n");

            _ = FixedWidth.Read<GenCodeModel>(kept, leaveOpen: true).ToList();
            _ = FixedWidth.Read<GenCodeModel>(owned).ToList();

            Assert.True(kept.CanRead);
            Assert.False(owned.CanRead);
        }

        [Fact]
        public void Read_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => FixedWidth.Read<GenCodeModel>(null!));
            Assert.Throws<ArgumentNullException>(() => FixedWidth.ReadAsync<GenCodeModel>((Stream)null!));
            Assert.Throws<ArgumentException>(() => FixedWidth.ReadFile<GenCodeModel>(""));
        }

        [Fact]
        public void Read_WithStringPool_InternsRepeatedValues()
        {
            var options = new FixedWidthReaderOptions { StringPool = new StringPool() };

            var codes = FixedWidth.Read<GenCodeModel>(StreamOf("ABC\nABC\n"), options: options).ToList();

            Assert.Same(codes[0].Code, codes[1].Code);
        }

        [Fact]
        public void GetEnumerator_IsStruct_ForAllocationFreeForeach()
        {
            Assert.True(typeof(RecordEnumerator<GenCodeModel, GeneratedLineParser<GenCodeModel>>).IsValueType);
        }

        [Fact]
        public void ReadFile_IsReEnumerable()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "ABC\nDEF\n");

                var first = FixedWidth.ReadFile<GenCodeModel>(path).Select(m => m.Code).ToList();
                var second = FixedWidth.ReadFile<GenCodeModel>(path).Select(m => m.Code).ToList();

                Assert.Equal(["ABC", "DEF"], first);
                Assert.Equal(first, second);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task ReadAsync_MatchesReflectionReader()
        {
            var expected = await FixedWidth.ReadAsync(StreamOf(TwoPeople), FixedWidthLayout.FromAttributes<PersonModel>()).ToListAsync(TestContext.Current.CancellationToken);
            var actual = await FixedWidth.ReadAsync<GenPersonModel>(StreamOf(TwoPeople)).ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(expected.Count, actual.Count);
            Assert.Equal(expected[1].Name, actual[1].Name);
            Assert.Equal(expected[1].Salary, actual[1].Salary, 2);
        }

        [Fact]
        public async Task ReadAsync_LeaveOpen_ControlsDisposal()
        {
            using var kept = StreamOf("ABC\n");
            var owned = StreamOf("ABC\n");

            _ = await FixedWidth.ReadAsync<GenCodeModel>(kept, leaveOpen: true).ToListAsync(TestContext.Current.CancellationToken);
            _ = await FixedWidth.ReadAsync<GenCodeModel>(owned).ToListAsync(TestContext.Current.CancellationToken);

            Assert.True(kept.CanRead);
            Assert.False(owned.CanRead);
        }

        [Fact]
        public async Task ReadAsync_CancelledToken_Throws()
        {
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in FixedWidth.ReadAsync<GenCodeModel>(StreamOf("ABC\nDEF\n")).WithCancellation(cts.Token))
                {
                }
            });
        }

        [Fact]
        public async Task ReadFileAsync_MatchesSyncReadFile()
        {
            string path = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(path, "ABC\nDEF\nGHI\n", TestContext.Current.CancellationToken);

                var asyncCodes = await FixedWidth.ReadFileAsync<GenCodeModel>(path).Select(m => m.Code).ToListAsync(TestContext.Current.CancellationToken);
                var syncCodes = FixedWidth.ReadFile<GenCodeModel>(path).Select(m => m.Code).ToList();

                Assert.Equal(["ABC", "DEF", "GHI"], asyncCodes);
                Assert.Equal(syncCodes, asyncCodes);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
