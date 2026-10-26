using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    public class CorrectnessTests
    {
        [Fact]
        public void DateTimeExact_RoundTrip_BothPaths()
        {
            var date = new DateTime(2026, 7, 16, 0, 0, 0, DateTimeKind.Unspecified);
            var dateOnly = new DateOnly(2026, 7, 16);
            const string input = "2026071620260716";

            var layout = FixedWidthLayout.FromAttributes<DateTimeExactModel>();
            Assert.True(FixedWidth.TryParse(Bytes(input), layout, Ctx, out var reflected));
            Assert.Equal(date, reflected.Date);
            Assert.Equal(dateOnly, reflected.DateOnlyVal);
            Assert.Equal(input, WriteOne(FixedWidthLayout.FromAttributes<DateTimeExactModel>(), reflected));

            Assert.True(FixedWidth.TryParse<GenDateTimeExactModel>(Bytes(input), Ctx, out var generated));
            Assert.Equal(date, generated.Date);
            Assert.Equal(dateOnly, generated.DateOnlyVal);
            Assert.Equal(input, Format(generated));
        }

        [Fact]
        public void ZeroPadded_Leading_TrimMode()
        {
            var layout = FixedWidthLayout.FromAttributes<ZeroPaddedLeadingModel>();
            Assert.True(FixedWidth.TryParse("0004203.14"u8, layout, Ctx, out var reflected));
            Assert.Equal(42, reflected.Value);
            Assert.Equal(3.14, reflected.DoubleValue);

            Assert.True(FixedWidth.TryParse<GenZeroPaddedLeadingModel>("0004203.14"u8, Ctx, out var generated));
            Assert.Equal(42, generated.Value);
            Assert.Equal(3.14, generated.DoubleValue);
        }

        [Fact]
        public void ZeroPadded_Both_TrimMode()
        {
            Assert.True(FixedWidth.TryParse("00420"u8, FixedWidthLayout.FromAttributes<ZeroPaddedBothModel>(), Ctx, out var reflected));
            Assert.Equal(42, reflected.Value);

            Assert.True(FixedWidth.TryParse<GenZeroPaddedBothModel>("00420"u8, Ctx, out var generated));
            Assert.Equal(42, generated.Value);
        }

        [Fact]
        public void Nullable_BlankDetection_WithCustomTrimChar()
        {
            const string spaces = "          ";
            const string padded = "00000*****";

            var layout = FixedWidthLayout.FromAttributes<NullablePaddingModel>();
            Assert.True(FixedWidth.TryParse(Bytes(spaces), layout, Ctx, out var fromSpaces));
            Assert.Null(fromSpaces.Value);
            Assert.Null(fromSpaces.DoubleValue);
            Assert.True(FixedWidth.TryParse(Bytes(padded), layout, Ctx, out var fromPadding));
            Assert.Null(fromPadding.Value);
            Assert.Null(fromPadding.DoubleValue);
            Assert.Equal(padded, WriteOne(FixedWidthLayout.FromAttributes<NullablePaddingModel>(), fromPadding));

            Assert.True(FixedWidth.TryParse<GenNullablePaddingModel>(Bytes(spaces), Ctx, out var generatedSpaces));
            Assert.Null(generatedSpaces.Value);
            Assert.Null(generatedSpaces.DoubleValue);
            Assert.True(FixedWidth.TryParse<GenNullablePaddingModel>(Bytes(padded), Ctx, out var generatedPadding));
            Assert.Null(generatedPadding.Value);
            Assert.Equal(padded, Format(generatedPadding));
        }

        [Theory]
        [InlineData(" 0 0 * * *")]
        [InlineData("0 0 0*** *")]
        [InlineData("  0  ** * ")]
        public void Nullable_BlankDetection_MixedSpacesAndTrimChar(string text)
        {
            Assert.True(FixedWidth.TryParse(Bytes(text), FixedWidthLayout.FromAttributes<NullablePaddingModel>(), Ctx, out var attributes));
            Assert.Null(attributes.Value);
            Assert.Null(attributes.DoubleValue);
            Assert.True(FixedWidth.TryParse<GenNullablePaddingModel>(Bytes(text), Ctx, out var generated));
            Assert.Null(generated.Value);
            Assert.Null(generated.DoubleValue);
        }

        [Fact]
        public void Generated_KeywordNames_ParseAndFormat()
        {
            Assert.True(FixedWidth.TryParse<@operator>(Bytes("42 7  "), Ctx, out var model));
            Assert.Equal(42, model.@event);
            Assert.Equal(7, model.@int);
            Assert.Equal("42 7  ", Format(model));
            Assert.True(FixedWidth.TryParse<@operator>(Bytes("42    "), Ctx, out var blank));
            Assert.Null(blank.@int);
        }

        [Fact]
        public void SignAware_ZeroPadding()
        {
            var layout = FixedWidthLayout.FromAttributes<SignPaddedModel>();

            Assert.Equal("-00042", WriteOne(layout, new SignPaddedModel { Value = -42 }));
            Assert.Equal("000042", WriteOne(layout, new SignPaddedModel { Value = 42 }));
        }

        [Fact]
        public void Writer_RejectsTruncateOnNonString()
        {
            Assert.Throws<InvalidOperationException>(() => FixedWidth.WriteMany(Stream.Null, Array.Empty<TruncateInvalidModel>(), FixedWidthLayout.FromAttributes<TruncateInvalidModel>()));
        }
    }
}
