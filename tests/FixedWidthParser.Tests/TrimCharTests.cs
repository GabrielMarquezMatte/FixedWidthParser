using System.Text;
using FixedWidthParser.Parsers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Covers <c>FixedColumnAttribute.TrimChar</c>: the character trimmed from the end of a column on
    /// the READ side (parsing) is now configurable, instead of hardcoded ' '. Exercises reflection
    /// parse (char + UTF-8), generated parity, and the non-ASCII-TrimChar guard on the UTF-8 byte path
    /// (mirrors the existing decimal-separator ASCII guard in <c>CultureHelpers</c>).
    /// </summary>
    public class TrimCharTests
    {
        [Fact]
        public void Reflection_Char_AsteriskTrim_ParsesUnderlyingValue()
        {
            var parser = new FixedWidthParser<AsteriskTrimIntModel>();

            bool ok = parser.TryParse("42***", Inv, null, out var model);

            Assert.True(ok);
            Assert.Equal(42, model.Value);
        }

        [Fact]
        public void Reflection_Char_HashTrim_StringColumn()
        {
            var parser = new FixedWidthParser<HashTrimStringModel>();

            bool ok = parser.TryParse("AB######", Inv, null, out var model);

            Assert.True(ok);
            Assert.Equal("AB", model.Code);
        }

        [Fact]
        public void Reflection_Utf8_AsteriskTrim_ParsesUnderlyingValue()
        {
            var parser = new Utf8FixedWidthParser<AsteriskTrimIntModel>();
            byte[] bytes = Encoding.UTF8.GetBytes("42***");

            bool ok = parser.TryParse(bytes, Inv, null, out var model);

            Assert.True(ok);
            Assert.Equal(42, model.Value);
        }

        [Fact]
        public void Reflection_Utf8_HashTrim_StringColumn()
        {
            var parser = new Utf8FixedWidthParser<HashTrimStringModel>();
            byte[] bytes = Encoding.UTF8.GetBytes("AB######");

            bool ok = parser.TryParse(bytes, Inv, null, out var model);

            Assert.True(ok);
            Assert.Equal("AB", model.Code);
        }

        [Fact]
        public void Reflection_Utf8_NonAsciiTrimChar_Throws()
        {
            Assert.Throws<NotSupportedException>(() => new Utf8FixedWidthParser<NonAsciiTrimModel>());
        }

        [Theory]
        [InlineData("42***")]
        [InlineData("1****")]
        public void Generated_Char_MatchesReflection(string line)
        {
            var reflection = new FixedWidthParser<AsteriskTrimIntModel>();

            bool okR = reflection.TryParse(line, Inv, null, out var r);
            bool okG = FixedWidth.TryParse<GenAsteriskTrimIntModel>(line, Inv, null, out var g);

            Assert.Equal(okR, okG);
            Assert.Equal(r.Value, g.Value);
        }

        [Fact]
        public void Generated_Utf8_MatchesReflection()
        {
            byte[] bytes = Encoding.UTF8.GetBytes("42***");
            var reflection = new Utf8FixedWidthParser<AsteriskTrimIntModel>();

            bool okR = reflection.TryParse(bytes, Inv, null, out var r);
            bool okG = FixedWidthUtf8.TryParse<GenAsteriskTrimIntModel>(bytes, Inv, null, out var g);

            Assert.Equal(okR, okG);
            Assert.Equal(r.Value, g.Value);
        }

        [Fact]
        public void Generated_Char_HashTrim_MatchesReflection()
        {
            const string line = "AB######";
            var reflection = new FixedWidthParser<HashTrimStringModel>();

            bool okR = reflection.TryParse(line, Inv, null, out var r);
            bool okG = FixedWidth.TryParse<GenHashTrimStringModel>(line, Inv, null, out var g);

            Assert.Equal(okR, okG);
            Assert.Equal(r.Code, g.Code);
        }

        [Fact]
        public void Generated_Utf8_NonAsciiTrimChar_Throws()
        {
            // The guard runs in the generated model's static field initializer (__trim0), so it
            // surfaces wrapped in a TypeInitializationException on first use of the type.
            byte[] bytes = Encoding.UTF8.GetBytes("   42");

            var ex = Assert.Throws<TypeInitializationException>(
                () => FixedWidthUtf8.TryParse<GenNonAsciiTrimModel>(bytes, Inv, null, out _));

            Assert.IsType<NotSupportedException>(ex.InnerException);
        }
    }
}
