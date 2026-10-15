using FixedWidthParser.Parsers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary><c>FixedColumnAttribute.TrimChar</c>: the character trimmed from a column when parsing.</summary>
    public class TrimCharTests
    {
        [Theory]
        [InlineData("42***", 42)]
        [InlineData("1****", 1)]
        public void AsteriskTrim_ParsesUnderlyingValue_OnBothPaths(string line, int expected)
        {
            Assert.True(new FixedWidthParser<AsteriskTrimIntModel>().TryParse(Bytes(line), Ctx, out var reflected));
            Assert.True(FixedWidth.TryParse<GenAsteriskTrimIntModel>(Bytes(line), Ctx, out var generated));

            Assert.Equal(expected, reflected.Value);
            Assert.Equal(expected, generated.Value);
        }

        [Fact]
        public void HashTrim_StringColumn_OnBothPaths()
        {
            Assert.True(new FixedWidthParser<HashTrimStringModel>().TryParse("AB######"u8, Ctx, out var reflected));
            Assert.True(FixedWidth.TryParse<GenHashTrimStringModel>("AB######"u8, Ctx, out var generated));

            Assert.Equal("AB", reflected.Code);
            Assert.Equal("AB", generated.Code);
        }

        [Fact]
        public void NonAsciiTrimChar_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => new FixedWidthParser<NonAsciiTrimModel>());
        }
    }
}
