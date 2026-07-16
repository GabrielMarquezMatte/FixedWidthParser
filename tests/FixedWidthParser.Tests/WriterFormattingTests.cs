using FixedWidthParser.Writers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Per-column configurable formatting: alignment, padding, format string and overflow.
    /// </summary>
    public class WriterFormattingTests
    {
        [Fact]
        public void Write_RightAligned_PadsOnLeftWithSpaces()
        {
            var writer = new FixedWidthWriter<RightAlignedModel>();

            string line = WriteOne(writer, new RightAlignedModel { Value = 30 });

            Assert.Equal("   30", line);
        }

        [Fact]
        public void Write_ZeroPaddedRightAligned_FillsWithZeros()
        {
            var writer = new FixedWidthWriter<ZeroPaddedModel>();

            string line = WriteOne(writer, new ZeroPaddedModel { Value = 30 });

            Assert.Equal("00030", line);
        }

        [Fact]
        public void Write_FormatString_IsApplied()
        {
            var writer = new FixedWidthWriter<FormattedModel>();

            string line = WriteOne(writer, new FormattedModel { Amount = 1234.5 });

            // "F2" → "1234.50" (7 chars), left-aligned in 8 → one padding char.
            Assert.Equal("1234.50 ", line);
        }

        [Fact]
        public void Write_RightAlignedString_PadsOnLeft()
        {
            var writer = new FixedWidthWriter<RightStringModel>();

            string line = WriteOne(writer, new RightStringModel { Code = "AB" });

            Assert.Equal("    AB", line);
        }

        [Fact]
        public void Write_NumericOverflow_ThrowsByDefault()
        {
            var writer = new FixedWidthWriter<NarrowModel>();

            var ex = Assert.Throws<InvalidOperationException>(
                () => WriteOne(writer, new NarrowModel { Value = 12345 }));

            Assert.Contains("Value", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Write_NumericOverflow_TruncatesWhenOptedIn()
        {
            Assert.Throws<InvalidOperationException>(() => new FixedWidthWriter<NarrowTruncateModel>());
        }

        [Fact]
        public void Write_FormattedValueLargerThanStackBuffer_UsesArrayPoolFallback()
        {
            var writer = new FixedWidthWriter<WideValueModel>();

            // 700 chars > 256 (stack buffer) and > 512 (first rented buffer): exercises the
            // ArrayPool fallback and its grow (size *= 2) path.
            string line = WriteOne(writer, new WideValueModel { Value = new RepeatedChar(700) });

            Assert.Equal(new string('X', 700) + new string(' ', 100), line);
        }

        [Fact]
        public void Write_FormattedValueJustOverStackBuffer_FitsFirstRentedBuffer()
        {
            var writer = new FixedWidthWriter<WideValueModel>();

            // 300 chars > 256 (stack) but <= 512: fallback succeeds on the first rented buffer.
            string line = WriteOne(writer, new WideValueModel { Value = new RepeatedChar(300) });

            Assert.Equal(new string('X', 300) + new string(' ', 500), line);
        }

        [Fact]
        public void Write_ValueThatNeverFits_ThrowsClearError()
        {
            var writer = new FixedWidthWriter<WideValueModel>();

            // 1<<21 chars exceeds the formatter's grow ceiling (1<<20), so TryFormat never succeeds.
            // The grow loop must fail fast with a clear, named error rather than overflowing its size
            // counter into a negative/huge ArrayPool rent.
            var ex = Assert.Throws<InvalidOperationException>(
                () => WriteOne(writer, new WideValueModel { Value = new RepeatedChar(1 << 21) }));

            Assert.Contains(nameof(WideValueModel.Value), ex.Message, StringComparison.Ordinal);
            Assert.Contains("could not be formatted", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Write_RightAlignedOverflow_TruncatesKeepingRightmostChars()
        {
            var writer = new FixedWidthWriter<RightTruncateModel>();

            // "ABCDEF" (6) in a right-aligned width-4 column → keeps the last 4 chars.
            string line = WriteOne(writer, new RightTruncateModel { Code = "ABCDEF" });

            Assert.Equal("CDEF", line);
        }

        [Fact]
        public void Write_StringOverflow_TruncatesByDefault()
        {
            // Default string behavior preserved (PersonModel.Name has width 10).
            var writer = new FixedWidthWriter<PersonModel>();

            string line = WriteOne(writer, new PersonModel { Name = "VeryLongName", Age = 1, Salary = 0 });

            Assert.Equal("VeryLongNa", line[..10]);
        }
    }
}
