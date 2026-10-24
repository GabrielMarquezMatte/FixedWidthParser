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
            var layout = FixedWidthLayout.FromAttributes<RightAlignedModel>();

            string line = WriteOne(layout, new RightAlignedModel { Value = 30 });

            Assert.Equal("   30", line);
        }

        [Fact]
        public void Write_ZeroPaddedRightAligned_FillsWithZeros()
        {
            var layout = FixedWidthLayout.FromAttributes<ZeroPaddedModel>();

            string line = WriteOne(layout, new ZeroPaddedModel { Value = 30 });

            Assert.Equal("00030", line);
        }

        [Fact]
        public void Write_FormatString_IsApplied()
        {
            var layout = FixedWidthLayout.FromAttributes<FormattedModel>();

            string line = WriteOne(layout, new FormattedModel { Amount = 1234.5 });

            // "F2" → "1234.50" (7 chars), left-aligned in 8 → one padding char.
            Assert.Equal("1234.50 ", line);
        }

        [Fact]
        public void Write_RightAlignedString_PadsOnLeft()
        {
            var layout = FixedWidthLayout.FromAttributes<RightStringModel>();

            string line = WriteOne(layout, new RightStringModel { Code = "AB" });

            Assert.Equal("    AB", line);
        }

        [Fact]
        public void Write_NumericOverflow_ThrowsByDefault()
        {
            var layout = FixedWidthLayout.FromAttributes<NarrowModel>();

            var ex = Assert.Throws<InvalidOperationException>(
                () => WriteOne(layout, new NarrowModel { Value = 12345 }));

            Assert.Contains("Value", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Write_NumericOverflow_TruncatesWhenOptedIn()
        {
            Assert.Throws<InvalidOperationException>(() => FixedWidth.WriteMany(Stream.Null, Array.Empty<NarrowTruncateModel>(), FixedWidthLayout.FromAttributes<NarrowTruncateModel>()));
        }

        [Fact]
        public void Write_FormattedValueLargerThanStackBuffer_UsesArrayPoolFallback()
        {
            var layout = FixedWidthLayout.FromAttributes<WideValueModel>();

            // 700 chars > 256 (stack buffer) and > 512 (first rented buffer): exercises the
            // ArrayPool fallback and its grow (size *= 2) path.
            string line = WriteOne(layout, new WideValueModel { Value = new RepeatedChar(700) });

            Assert.Equal(new string('X', 700) + new string(' ', 100), line);
        }

        [Fact]
        public void Write_FormattedValueJustOverStackBuffer_FitsFirstRentedBuffer()
        {
            var layout = FixedWidthLayout.FromAttributes<WideValueModel>();

            // 300 chars > 256 (stack) but <= 512: fallback succeeds on the first rented buffer.
            string line = WriteOne(layout, new WideValueModel { Value = new RepeatedChar(300) });

            Assert.Equal(new string('X', 300) + new string(' ', 500), line);
        }

        [Fact]
        public void Write_ValueThatNeverFits_ThrowsClearError()
        {
            var layout = FixedWidthLayout.FromAttributes<WideValueModel>();

            // 1<<21 chars exceeds the formatter's grow ceiling (1<<20), so TryFormat never succeeds.
            // The grow loop must fail fast with a clear, named error rather than overflowing its size
            // counter into a negative/huge ArrayPool rent.
            var ex = Assert.Throws<InvalidOperationException>(
                () => WriteOne(layout, new WideValueModel { Value = new RepeatedChar(1 << 21) }));

            Assert.Contains(nameof(WideValueModel.Value), ex.Message, StringComparison.Ordinal);
            Assert.Contains("could not be formatted", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Write_RightAlignedOverflow_TruncatesKeepingRightmostChars()
        {
            var layout = FixedWidthLayout.FromAttributes<RightTruncateModel>();

            // "ABCDEF" (6) in a right-aligned width-4 column → keeps the last 4 chars.
            string line = WriteOne(layout, new RightTruncateModel { Code = "ABCDEF" });

            Assert.Equal("CDEF", line);
        }

        [Fact]
        public void Write_StringOverflow_TruncatesByDefault()
        {
            // Default string behavior preserved (PersonModel.Name has width 10).
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();

            string line = WriteOne(layout, new PersonModel { Name = "VeryLongName", Age = 1, Salary = 0 });

            Assert.Equal("VeryLongNa", line[..10]);
        }
    }
}
