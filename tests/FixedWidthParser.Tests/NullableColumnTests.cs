using System.Text;
using FixedWidthParser.Parsers;
using FixedWidthParser.Writers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Covers nullable value-type columns (<c>T?</c>): a blank (all-spaces) column parses to
    /// <see langword="null"/> without invoking the underlying parser, and a present value round-trips
    /// exactly as the non-nullable <c>T</c> would. Exercises reflection parse/write (char + UTF-8) and
    /// the generated parity, plus the combination with a custom converter.
    /// </summary>
    public class NullableColumnTests
    {
        // NullableModel is Age[0,5) + Amount[5,15): build lines by padding each field to its column
        // width, rather than hand-counting spaces in a literal (an easy off-by-one to get wrong).
        private static string Line(string age, string amount)
        {
            return age.PadRight(5) + amount.PadRight(10);
        }

        private static readonly string Blank = Line("", "");

        [Fact]
        public void Reflection_Char_BlankColumn_ParsesToNull()
        {
            var parser = new FixedWidthParser<NullableModel>();

            bool ok = parser.TryParse(Blank, Inv, null, out var model);

            Assert.True(ok);
            Assert.Null(model.Age);
            Assert.Null(model.Amount);
        }

        [Fact]
        public void Reflection_Char_PresentValue_Parses()
        {
            var parser = new FixedWidthParser<NullableModel>();

            bool ok = parser.TryParse(Line("30", "123.45"), Inv, null, out var model);

            Assert.True(ok);
            Assert.Equal(30, model.Age);
            Assert.Equal(123.45m, model.Amount);
        }

        [Fact]
        public void Reflection_Char_InvalidValue_ReturnsFalse()
        {
            var parser = new FixedWidthParser<NullableModel>();

            bool ok = parser.TryParse(Line("XX", ""), Inv, null, out _);

            Assert.False(ok);
        }

        [Fact]
        public void Reflection_Utf8_BlankColumn_ParsesToNull()
        {
            var parser = new Utf8FixedWidthParser<NullableModel>();
            byte[] bytes = Encoding.UTF8.GetBytes(Blank);

            bool ok = parser.TryParse(bytes, Inv, null, out var model);

            Assert.True(ok);
            Assert.Null(model.Age);
            Assert.Null(model.Amount);
        }

        [Fact]
        public void Reflection_Utf8_PresentValue_Parses()
        {
            var parser = new Utf8FixedWidthParser<NullableModel>();
            byte[] bytes = Encoding.UTF8.GetBytes(Line("30", "123.45"));

            bool ok = parser.TryParse(bytes, Inv, null, out var model);

            Assert.True(ok);
            Assert.Equal(30, model.Age);
            Assert.Equal(123.45m, model.Amount);
        }

        [Fact]
        public void Reflection_Write_Null_WritesBlank()
        {
            var writer = new FixedWidthWriter<NullableModel>();
            var model = new NullableModel { Age = null, Amount = null };

            string line = WriteOne(writer, model);

            Assert.Equal(Blank, line);
        }

        [Fact]
        public void Reflection_RoundTrip_PreservesNullAndValue()
        {
            var writer = new FixedWidthWriter<NullableModel>();
            var parser = new FixedWidthParser<NullableModel>();
            var original = new NullableModel { Age = 42, Amount = null };

            string line = WriteOne(writer, original);
            bool ok = parser.TryParse(line, Inv, null, out var parsed);

            Assert.True(ok);
            Assert.Equal(original.Age, parsed.Age);
            Assert.Null(parsed.Amount);
        }

        [Theory]
        [InlineData("               ")]
        [InlineData("30   123.45    ")]
        [InlineData("XX             ")]
        public void Generated_Char_MatchesReflection(string line)
        {
            var reflection = new FixedWidthParser<NullableModel>();

            bool okR = reflection.TryParse(line, Inv, null, out var r);
            bool okG = FixedWidth.TryParse<GenNullableModel>(line, Inv, null, out var g);

            Assert.Equal(okR, okG);
            if (okR)
            {
                Assert.Equal(r.Age, g.Age);
                Assert.Equal(r.Amount, g.Amount);
            }
        }

        [Theory]
        [InlineData("               ")]
        [InlineData("30   123.45    ")]
        public void Generated_Utf8_MatchesReflection(string line)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(line);
            var reflection = new Utf8FixedWidthParser<NullableModel>();

            bool okR = reflection.TryParse(bytes, Inv, null, out var r);
            bool okG = FixedWidthUtf8.TryParse<GenNullableModel>(bytes, Inv, null, out var g);

            Assert.Equal(okR, okG);
            Assert.Equal(r.Age, g.Age);
            Assert.Equal(r.Amount, g.Amount);
        }

        [Fact]
        public void NullableConverter_Blank_ParsesToNull()
        {
            var parser = new FixedWidthParser<NullableConverterModel>();

            bool ok = parser.TryParse("        ", Inv, null, out var model);

            Assert.True(ok);
            Assert.Null(model.Amount);
        }

        [Fact]
        public void NullableConverter_PresentValue_Parses()
        {
            var parser = new FixedWidthParser<NullableConverterModel>();

            bool ok = parser.TryParse("12345   ", Inv, null, out var model);

            Assert.True(ok);
            Assert.Equal(new CentsValue(12345), model.Amount);
        }

        [Fact]
        public void NullableConverter_Write_Null_WritesBlank()
        {
            var writer = new FixedWidthWriter<NullableConverterModel>();
            var model = new NullableConverterModel { Amount = null };

            string line = WriteOne(writer, model);

            Assert.Equal(new string(' ', 8), line);
        }

        [Fact]
        public void NullableConverter_Generated_Char_MatchesReflection()
        {
            var reflection = new FixedWidthParser<NullableConverterModel>();
            const string line = "12345   ";

            bool okR = reflection.TryParse(line, Inv, null, out var r);
            bool okG = FixedWidth.TryParse<GenNullableConverterModel>(line, Inv, null, out var g);

            Assert.Equal(okR, okG);
            Assert.Equal(r.Amount, g.Amount);
        }

        [Fact]
        public void NullableConverter_Generated_Char_Blank_MatchesReflection()
        {
            var reflection = new FixedWidthParser<NullableConverterModel>();
            const string line = "        ";

            bool okR = reflection.TryParse(line, Inv, null, out var r);
            bool okG = FixedWidth.TryParse<GenNullableConverterModel>(line, Inv, null, out var g);

            Assert.Equal(okR, okG);
            Assert.Null(r.Amount);
            Assert.Null(g.Amount);
        }
    }
}
