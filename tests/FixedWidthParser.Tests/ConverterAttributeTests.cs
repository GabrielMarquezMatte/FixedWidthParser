using System.Text;
using FixedWidthParser.Parsers;
using FixedWidthParser.Writers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Covers <see cref="Attributes.FixedColumnAttribute.Converter"/>: a per-property converter that
    /// takes priority over the built-in <c>ISpanParsable</c> fallback, across all four combinations of
    /// reflection/generated and char/UTF-8, plus the write side and the build-time mismatch check.
    /// </summary>
    public class ConverterAttributeTests
    {
        [Fact]
        public void Reflection_Char_RoundTrips()
        {
            var writer = new FixedWidthWriter<CentsConverterModel>();
            var parser = new FixedWidthParser<CentsConverterModel>();
            var original = new CentsConverterModel { Amount = new CentsValue(12345) };

            string line = WriteOne(writer, original);
            bool ok = parser.TryParse(line, Inv, null, out var parsed);

            Assert.True(ok);
            Assert.Equal(original.Amount, parsed.Amount);
        }

        [Fact]
        public void Reflection_Utf8_Parses()
        {
            var parser = new Utf8FixedWidthParser<CentsConverterModel>();
            byte[] bytes = Encoding.UTF8.GetBytes("12345   ");

            bool ok = parser.TryParse(bytes, Inv, null, out var parsed);

            Assert.True(ok);
            Assert.Equal(new CentsValue(12345), parsed.Amount);
        }

        [Fact]
        public void Generated_Char_MatchesReflection()
        {
            var reflection = new FixedWidthParser<CentsConverterModel>();
            const string line = "12345   ";

            bool okR = reflection.TryParse(line, Inv, null, out var r);
            bool okG = FixedWidth.TryParse<GenCentsConverterModel>(line, Inv, null, out var g);

            Assert.Equal(okR, okG);
            Assert.Equal(r.Amount, g.Amount);
        }

        [Fact]
        public void Generated_Utf8_MatchesReflection()
        {
            byte[] bytes = Encoding.UTF8.GetBytes("12345   ");
            var reflection = new Utf8FixedWidthParser<CentsConverterModel>();

            bool okR = reflection.TryParse(bytes, Inv, null, out var r);
            bool okG = FixedWidthUtf8.TryParse<GenCentsConverterModel>(bytes, Inv, null, out var g);

            Assert.Equal(okR, okG);
            Assert.Equal(r.Amount, g.Amount);
        }

        [Fact]
        public void Converter_RejectingInput_ReturnsFalse()
        {
            var parser = new FixedWidthParser<CentsConverterModel>();

            bool ok = parser.TryParse("notanum ", Inv, null, out _);

            Assert.False(ok);
        }

        [Fact]
        public void MismatchedConverter_ParserConstruction_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => new FixedWidthParser<MismatchedConverterModel>());
        }

        [Fact]
        public void MismatchedConverter_WriterConstruction_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => new FixedWidthWriter<MismatchedConverterModel>());
        }
    }
}
