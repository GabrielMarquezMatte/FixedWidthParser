using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// <see cref="Attributes.FixedColumnAttribute.Converter"/>: a per-property converter that takes
    /// priority over the built-in parsers, on the reflection and generated paths, for parse and write,
    /// plus the build-time mismatch check.
    /// </summary>
    public class ConverterAttributeTests
    {
        [Fact]
        public void Reflection_RoundTrips()
        {
            var original = new CentsConverterModel { Amount = new CentsValue(12345) };

            string line = WriteOne(FixedWidthLayout.FromAttributes<CentsConverterModel>(), original);

            Assert.True(FixedWidth.TryParse(Bytes(line), FixedWidthLayout.FromAttributes<CentsConverterModel>(), Ctx, out var parsed));
            Assert.Equal(original.Amount, parsed.Amount);
        }

        [Fact]
        public void Generated_MatchesReflection()
        {
            bool okR = FixedWidth.TryParse("12345   "u8, FixedWidthLayout.FromAttributes<CentsConverterModel>(), Ctx, out var r);
            bool okG = FixedWidth.TryParse<GenCentsConverterModel>("12345   "u8, Ctx, out var g);

            Assert.True(okR);
            Assert.True(okG);
            Assert.Equal(r.Amount, g.Amount);
        }

        [Fact]
        public void Converter_RejectingInput_ReturnsFalse()
        {
            Assert.False(FixedWidth.TryParse("notanum "u8, FixedWidthLayout.FromAttributes<CentsConverterModel>(), Ctx, out _));
            Assert.False(FixedWidth.TryParse<GenCentsConverterModel>("notanum "u8, Ctx, out _));
        }

        [Fact]
        public void MismatchedConverter_ParserConstruction_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => FixedWidth.TryParse(ReadOnlySpan<byte>.Empty, FixedWidthLayout.FromAttributes<MismatchedConverterModel>(), Ctx, out _));
        }

        [Fact]
        public void MismatchedConverter_WriterConstruction_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => FixedWidth.WriteMany(Stream.Null, Array.Empty<MismatchedConverterModel>(), FixedWidthLayout.FromAttributes<MismatchedConverterModel>()));
        }
    }
}
