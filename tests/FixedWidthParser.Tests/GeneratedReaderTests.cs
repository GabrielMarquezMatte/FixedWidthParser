using System.Globalization;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Readers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    public class GeneratedReaderTests
    {
        private const string TwoPeople =
            "John Doe  30   60000.00  \n" +
            "Jane      28   55000.00  ";

        [Fact]
        public void Read_MultipleLines_MatchesReflectionReader()
        {
            var reflection = new FixedWidthReader<PersonModel>(Inv);

            var expected = reflection.Read(new StringReader(TwoPeople)).ToList();
            var actual = FixedWidth.Read<GenPersonModel>(
                new StringReader(TwoPeople),
                formatProvider: Inv).ToList();

            Assert.Equal(expected.Count, actual.Count);
            Assert.Equal(expected[0].Name, actual[0].Name);
            Assert.Equal(expected[0].Age, actual[0].Age);
            Assert.Equal(expected[0].Salary, actual[0].Salary, 2);
            Assert.Equal(expected[1].Name, actual[1].Name);
            Assert.Equal(expected[1].Age, actual[1].Age);
            Assert.Equal(expected[1].Salary, actual[1].Salary, 2);
        }

        [Theory]
        [InlineData("ABC\nDEF\n")]
        [InlineData("ABC\r\nDEF\r\n")]
        [InlineData("ABC\nDEF")]
        public void Read_HandlesLineEndingsAndTrailingNewline(string text)
        {
            var codes = FixedWidth.Read<GenCodeModel>(new StringReader(text))
                .Select(m => m.Code)
                .ToList();

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public void Read_SkipsEmptyLines()
        {
            var codes = FixedWidth.Read<GenCodeModel>(new StringReader("ABC\n\n\nDEF\n"))
                .Select(m => m.Code)
                .ToList();

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public void Read_LineLongerThanBuffer_MatchesReflectionReader()
        {
            var reflection = new FixedWidthReader<PersonModel>(Inv, bufferSize: 4);

            var expected = reflection.Read(new StringReader(TwoPeople)).ToList();
            var actual = FixedWidth.Read<GenPersonModel>(
                new StringReader(TwoPeople),
                formatProvider: Inv,
                bufferSize: 4).ToList();

            Assert.Equal(expected[0].Name, actual[0].Name);
            Assert.Equal(expected[1].Name, actual[1].Name);
            Assert.Equal(expected[1].Salary, actual[1].Salary, 2);
        }

        [Fact]
        public void Read_InvalidLine_ThrowsWithSameLineNumber()
        {
            var reflection = new FixedWidthReader<PersonModel>(Inv);
            var text = "John Doe  30   60000.00  \nJane      XX   55000.00  ";

            var expected = Assert.Throws<FormatException>(
                () => reflection.Read(new StringReader(text)).ToList());
            var actual = Assert.Throws<FormatException>(
                () => FixedWidth.Read<GenPersonModel>(new StringReader(text), formatProvider: Inv).ToList());

            Assert.Contains("Line 2", expected.Message);
            Assert.Contains("Line 2", actual.Message);
        }

        [Fact]
        public void Read_WithStringPool_InternsRepeatedValues()
        {
            var pool = new StringPool();

            var codes = FixedWidth.Read<GenCodeModel>(
                new StringReader("ABC\nABC\n"),
                stringPool: pool).ToList();

            Assert.Equal("ABC", codes[0].Code);
            Assert.Same(codes[0].Code, codes[1].Code);
        }

        [Fact]
        public void GetEnumerator_IsStruct_ForAllocationFreeForeach()
        {
            Assert.True(typeof(GeneratedFixedWidthRecordEnumerable<GenCodeModel>.Enumerator).IsValueType);

            int count = 0;
            foreach (var _ in FixedWidth.Read<GenCodeModel>(new StringReader("ABC\nDEF\n"))) count++;

            Assert.Equal(2, count);
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
        public async Task ReadAsync_MultipleLines_MatchesReflectionReader()
        {
            var reflection = new FixedWidthReader<PersonModel>(Inv);
            var expected = await reflection.ReadAsync(new StringReader(TwoPeople)).ToListAsync();
            var actual = await FixedWidth.ReadAsync<GenPersonModel>(
                new StringReader(TwoPeople),
                formatProvider: Inv).ToListAsync();

            Assert.Equal(expected.Count, actual.Count);
            Assert.Equal(expected[0].Name, actual[0].Name);
            Assert.Equal(expected[0].Age, actual[0].Age);
            Assert.Equal(expected[1].Name, actual[1].Name);
            Assert.Equal(expected[1].Salary, actual[1].Salary, 2);
        }

        [Fact]
        public async Task ReadAsync_LineLongerThanBuffer_MatchesReflectionReader()
        {
            var reflection = new FixedWidthReader<PersonModel>(Inv, bufferSize: 4);
            var expected = await reflection.ReadAsync(new StringReader(TwoPeople)).ToListAsync();
            var actual = await FixedWidth.ReadAsync<GenPersonModel>(
                new StringReader(TwoPeople),
                formatProvider: Inv,
                bufferSize: 4).ToListAsync();

            Assert.Equal(expected[0].Name, actual[0].Name);
            Assert.Equal(expected[1].Salary, actual[1].Salary, 2);
        }

        [Fact]
        public async Task ReadAsync_InvalidLine_ThrowsWithSameLineNumber()
        {
            var reflection = new FixedWidthReader<PersonModel>(Inv);
            var text = "John Doe  30   60000.00  \nJane      XX   55000.00  ";

            var expected = await Assert.ThrowsAsync<FormatException>(async () =>
            {
                await foreach (var _ in reflection.ReadAsync(new StringReader(text))) { }
            });
            var actual = await Assert.ThrowsAsync<FormatException>(async () =>
            {
                await foreach (var _ in FixedWidth.ReadAsync<GenPersonModel>(new StringReader(text), formatProvider: Inv)) { }
            });

            Assert.Contains("Line 2", expected.Message);
            Assert.Contains("Line 2", actual.Message);
        }

        [Fact]
        public async Task ReadFileAsync_MatchesSyncReadFile()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "ABC\nDEF\nGHI\n");

                var asyncCodes = await FixedWidth.ReadFileAsync<GenCodeModel>(path).Select(m => m.Code).ToListAsync();
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
