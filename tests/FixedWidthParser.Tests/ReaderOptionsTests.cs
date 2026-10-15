using System.IO.Pipelines;
using System.Text;
using FixedWidthParser.Readers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// <see cref="FixedWidthReaderOptions"/>: the column a <see cref="FixedWidthParseException"/> names,
    /// <see cref="FixedWidthReaderOptions.OnParseError"/>, <see cref="FixedWidthReaderOptions.MaxLineLength"/>
    /// and validation.
    /// </summary>
    public class ReaderOptionsTests
    {
        private const string BadAge =
            "John Doe  30   60000.00  \n" +
            "Jane      XX   55000.00  \n" +
            "Max       41   72000.00  \n";

        [Fact]
        public void ParseError_NamesColumnAndRawValue_OnBothPaths()
        {
            var reflected = Assert.Throws<FixedWidthParseException>(() => new FixedWidthReader<PersonModel>().Read(StreamOf(BadAge)).ToList());
            var generated = Assert.Throws<FixedWidthParseException>(() => FixedWidth.Read<GenPersonModel>(StreamOf(BadAge)).ToList());

            foreach (var ex in new[] { reflected, generated })
            {
                Assert.Equal(2, ex.LineNumber);
                Assert.Equal("Age", ex.ColumnName);
                Assert.Equal(10, ex.ColumnStart);
                Assert.Equal(5, ex.ColumnLength);
                Assert.Equal("XX   ", ex.RawValue);
                Assert.Contains("column 'Age'", ex.Message, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void ParseError_ShortLine_SaysHowLongTheLayoutIs()
        {
            var reflected = Assert.Throws<FixedWidthParseException>(() => new FixedWidthReader<PersonModel>().Read(StreamOf("John\n")).ToList());
            var generated = Assert.Throws<FixedWidthParseException>(() => FixedWidth.Read<GenPersonModel>(StreamOf("John\n")).ToList());

            foreach (var ex in new[] { reflected, generated })
            {
                Assert.Null(ex.ColumnName);
                Assert.Equal(-1, ex.ColumnStart);
                Assert.Contains("is 4 bytes long and the layout needs 25", ex.Message, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void ParseError_IsAFormatException()
        {
            Assert.ThrowsAny<FormatException>(() => new FixedWidthReader<PersonModel>().Read(StreamOf(BadAge)).ToList());
        }

        [Fact]
        public void OnParseError_ReturningTrue_SkipsTheLine()
        {
            var errors = new List<FixedWidthParseException>();
            var options = new FixedWidthReaderOptions { OnParseError = ex => { errors.Add(ex); return true; } };

            var reflected = new FixedWidthReader<PersonModel>(options).Read(StreamOf(BadAge)).Select(p => p.Name).ToList();
            var generated = FixedWidth.Read<GenPersonModel>(StreamOf(BadAge), options: options).Select(p => p.Name).ToList();

            Assert.Equal(["John Doe", "Max"], reflected);
            Assert.Equal(reflected, generated);
            Assert.Equal(2, errors.Count);
            Assert.All(errors, ex => Assert.Equal(2, ex.LineNumber));
        }

        [Fact]
        public void OnParseError_ReturningFalse_Throws()
        {
            var options = new FixedWidthReaderOptions { OnParseError = _ => false };

            Assert.Throws<FixedWidthParseException>(() => new FixedWidthReader<PersonModel>(options).Read(StreamOf(BadAge)).ToList());
        }

        [Fact]
        public void OnParseError_SkippingTheLastLine_EndsCleanly()
        {
            var options = new FixedWidthReaderOptions { OnParseError = _ => true };

            var codes = new FixedWidthReader<CodeModel>(options).Read(StreamOf("ABC\nA")).Select(m => m.Code).ToList();

            Assert.Equal(["ABC"], codes);
        }

        [Fact]
        public async Task OnParseError_SkipsOnAsyncAndPipeReads()
        {
            var options = new FixedWidthReaderOptions { OnParseError = _ => true };
            var reader = new FixedWidthReader<PersonModel>(options);

            var fromStream = await reader.ReadAsync(StreamOf(BadAge)).Select(p => p.Name).ToListAsync(TestContext.Current.CancellationToken);
            var fromPipe = await reader.ReadAsync(PipeReader.Create(StreamOf(BadAge))).Select(p => p.Name).ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(["John Doe", "Max"], fromStream);
            Assert.Equal(fromStream, fromPipe);
        }

        [Fact]
        public void MaxLineLength_UnterminatedLine_Throws()
        {
            // No line break at all: the buffer would otherwise keep doubling.
            var options = new FixedWidthReaderOptions { BufferSize = 16, MaxLineLength = 100 };
            var source = new MemoryStream(new byte[10_000]);

            var ex = Assert.Throws<InvalidDataException>(() => new FixedWidthReader<CodeModel>(options).Read(source).ToList());

            Assert.Contains("MaxLineLength", ex.Message, StringComparison.Ordinal);
            Assert.Contains("Line 1", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void MaxLineLength_TerminatedLongLine_Throws()
        {
            var options = new FixedWidthReaderOptions { MaxLineLength = 5 };

            var ex = Assert.Throws<InvalidDataException>(() => new FixedWidthReader<CodeModel>(options).Read(StreamOf("ABC\nABCDEFGH\n")).ToList());

            Assert.Contains("Line 2", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void MaxLineLength_LineAtTheLimit_IsRead()
        {
            var options = new FixedWidthReaderOptions { BufferSize = 2, MaxLineLength = 3 };

            var codes = new FixedWidthReader<CodeModel>(options).Read(StreamOf("ABC\r\nDEF")).Select(m => m.Code).ToList();

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public async Task MaxLineLength_UnterminatedLine_ThrowsOnAsyncRead()
        {
            var options = new FixedWidthReaderOptions { BufferSize = 16, MaxLineLength = 100 };

            await Assert.ThrowsAsync<InvalidDataException>(async () =>
            {
                await foreach (var _ in new FixedWidthReader<CodeModel>(options).ReadAsync(new MemoryStream(new byte[10_000])))
                {
                }
            });
        }

        [Fact]
        public async Task MaxLineLength_UnterminatedLine_ThrowsOnPipeRead()
        {
            var options = new FixedWidthReaderOptions { MaxLineLength = 100 };
            var pipe = PipeReader.Create(new MemoryStream(new byte[10_000]), new StreamPipeReaderOptions(bufferSize: 64));

            await Assert.ThrowsAsync<InvalidDataException>(async () =>
            {
                await foreach (var _ in FixedWidth.ReadAsync<GenCodeModel>(pipe, options: options))
                {
                }
            });
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(10, 0)]
        public void Options_OutOfRange_Throw(int bufferSize, int maxLineLength)
        {
            var options = new FixedWidthReaderOptions { BufferSize = bufferSize, MaxLineLength = maxLineLength };

            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedWidthReader<CodeModel>(options));
            Assert.Throws<ArgumentOutOfRangeException>(() => FixedWidth.Read<GenCodeModel>(StreamOf("ABC"), options: options));
        }

        [Fact]
        public void Latin1Encoding_DoesNotSkipWhatLooksLikeAUtf8Bom()
        {
            // In Latin-1, EF BB BF are the characters "ï»¿", not a byte order mark.
            var options = new FixedWidthReaderOptions { Encoding = Encoding.Latin1 };
            byte[] bytes = [0xEF, 0xBB, 0xBF, .. "\nABC\n"u8];

            var codes = new FixedWidthReader<CodeModel>(options).Read(new MemoryStream(bytes)).Select(m => m.Code).ToList();

            Assert.Equal(["ï»¿", "ABC"], codes);
        }

        [Fact]
        public void Utf8Text_WithMultiByteCharacters_KeepsBytePositions()
        {
            // "Zoë" is 4 bytes in UTF-8; the layout counts bytes, so the name column must be 6 wide here
            // and the age still starts at byte 6.
            var person = new FixedWidthReader<NameAgeModel>().Read(StreamOf("Zoë  42 \n")).Single();

            Assert.Equal("Zoë", person.Name);
            Assert.Equal(42, person.Age);
        }
    }
}
