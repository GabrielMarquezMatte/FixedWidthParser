using FixedWidthParser.Parsers;
using FixedWidthParser.Writers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Nullable value-type columns (<c>T?</c>): a blank column parses to <see langword="null"/> without
    /// invoking the underlying parser, and a present value round-trips exactly as <c>T</c> would.
    /// </summary>
    public class NullableColumnTests
    {
        // NullableModel is Age[0,5) + Amount[5,15).
        private static byte[] Line(string age, string amount)
        {
            return Bytes(age.PadRight(5) + amount.PadRight(10));
        }

        [Fact]
        public void BlankColumn_ParsesToNull()
        {
            Assert.True(new FixedWidthParser<NullableModel>().TryParse(Line("", ""), Ctx, out var model));
            Assert.Null(model.Age);
            Assert.Null(model.Amount);
        }

        [Fact]
        public void PresentValue_Parses()
        {
            Assert.True(new FixedWidthParser<NullableModel>().TryParse(Line("30", "123.45"), Ctx, out var model));
            Assert.Equal(30, model.Age);
            Assert.Equal(123.45m, model.Amount);
        }

        [Fact]
        public void InvalidValue_ReturnsFalse()
        {
            Assert.False(new FixedWidthParser<NullableModel>().TryParse(Line("XX", ""), Ctx, out _));
        }

        [Fact]
        public void Write_Null_WritesBlank()
        {
            string line = WriteOne(new FixedWidthWriter<NullableModel>(), new NullableModel { Age = null, Amount = null });

            Assert.Equal(new string(' ', 15), line);
        }

        [Fact]
        public void RoundTrip_PreservesNullAndValue()
        {
            var original = new NullableModel { Age = 42, Amount = null };

            string line = WriteOne(new FixedWidthWriter<NullableModel>(), original);

            Assert.True(new FixedWidthParser<NullableModel>().TryParse(Bytes(line), Ctx, out var parsed));
            Assert.Equal(original.Age, parsed.Age);
            Assert.Null(parsed.Amount);
        }

        [Theory]
        [InlineData("               ")]
        [InlineData("30   123.45    ")]
        [InlineData("XX             ")]
        public void Generated_MatchesReflection(string line)
        {
            bool okR = new FixedWidthParser<NullableModel>().TryParse(Bytes(line), Ctx, out var r);
            bool okG = FixedWidth.TryParse<GenNullableModel>(Bytes(line), Ctx, out var g);

            Assert.Equal(okR, okG);
            Assert.Equal(r.Age, g.Age);
            Assert.Equal(r.Amount, g.Amount);
        }

        [Theory]
        [InlineData("        ")]
        [InlineData("12345   ")]
        public void NullableConverter_Generated_MatchesReflection(string line)
        {
            bool okR = new FixedWidthParser<NullableConverterModel>().TryParse(Bytes(line), Ctx, out var r);
            bool okG = FixedWidth.TryParse<GenNullableConverterModel>(Bytes(line), Ctx, out var g);

            Assert.True(okR);
            Assert.True(okG);
            Assert.Equal(r.Amount, g.Amount);
        }

        [Fact]
        public void NullableConverter_PresentValue_Parses()
        {
            Assert.True(new FixedWidthParser<NullableConverterModel>().TryParse("12345   "u8, Ctx, out var model));
            Assert.Equal(new CentsValue(12345), model.Amount);
        }

        [Fact]
        public void NullableConverter_Write_Null_WritesBlank()
        {
            string line = WriteOne(new FixedWidthWriter<NullableConverterModel>(), new NullableConverterModel { Amount = null });

            Assert.Equal(new string(' ', 8), line);
        }
    }
}
