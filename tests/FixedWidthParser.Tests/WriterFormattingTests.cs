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

            Assert.Contains("Value", ex.Message);
        }

        [Fact]
        public void Write_NumericOverflow_TruncatesWhenOptedIn()
        {
            var writer = new FixedWidthWriter<NarrowTruncateModel>();

            string line = WriteOne(writer, new NarrowTruncateModel { Value = 12345 });

            // Left alignment → keeps the first characters.
            Assert.Equal("123", line);
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
