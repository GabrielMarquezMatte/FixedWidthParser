using System.Globalization;
using System.Text;
using FixedWidthParser;
using FixedWidthParser.Attributes;
using FixedWidthParser.Formatters;
using FixedWidthParser.Parsers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Fills coverage gaps left by the type-focused suites: the TimeOnly/DateTimeOffset exact-parser
    /// branches (reflection factories and generated runtime helpers), the UTF-8 culture parser's
    /// wide-field ArrayPool transcode branch, and the converter formatter's ArrayPool growth/exhaustion.
    /// </summary>
    public class CoverageTests
    {
        private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

        // ----- TimeOnly + DateTimeOffset exact parsing (both factories + both generated runtimes) -----

        [Fact]
        public void TimeExact_RoundTrip_AllPaths()
        {
            var time = new TimeOnly(13, 45, 30);
            var timestamp = new DateTimeOffset(2026, 7, 16, 13, 45, 30, TimeSpan.FromHours(3));
            const string input = "13453020260716134530+03:00";
            var bytes = Encoding.UTF8.GetBytes(input);

            // Reflection char (ColumnParserFactory.BuildExact: TimeOnly + DateTimeOffset branches)
            var pChar = new FixedWidthParser<TimeExactModel>();
            Assert.True(pChar.TryParse(input, Inv, null, out var m1));
            Assert.Equal(time, m1.Time);
            Assert.Equal(timestamp, m1.Timestamp);

            // Reflection UTF-8 (Utf8ColumnParserFactory.BuildExact)
            var pUtf8 = new Utf8FixedWidthParser<TimeExactModel>();
            Assert.True(pUtf8.TryParse(bytes, Inv, null, out var m2));
            Assert.Equal(time, m2.Time);
            Assert.Equal(timestamp, m2.Timestamp);

            // Generated char (FixedWidthRuntime.TryTimeOnlyExact / TryDateTimeOffsetExact)
            Assert.True(FixedWidth.TryParse<GenTimeExactModel>(input, Inv, null, out var m3));
            Assert.Equal(time, m3.Time);
            Assert.Equal(timestamp, m3.Timestamp);

            // Generated UTF-8 (Utf8FixedWidthRuntime.TryTimeOnlyExact / TryDateTimeOffsetExact)
            Assert.True(FixedWidthUtf8.TryParse<GenTimeExactModel>(bytes, Inv, null, out var m4));
            Assert.Equal(time, m4.Time);
            Assert.Equal(timestamp, m4.Timestamp);
        }

        [Fact]
        public void TimeExact_InvalidField_ReturnsFalse()
        {
            // A non-time first column makes the exact parser return false (the failure branch in both factories).
            const string bad = "xxxxxx20260716134530+03:00";
            var badBytes = Encoding.UTF8.GetBytes(bad);

            Assert.False(new FixedWidthParser<TimeExactModel>().TryParse(bad, Inv, null, out _));
            Assert.False(new Utf8FixedWidthParser<TimeExactModel>().TryParse(badBytes, Inv, null, out _));
        }

        [Fact]
        public void Utf8_ConverterRejectsField_ReturnsFalse()
        {
            // A non-numeric field makes CentsConverter.TryParse (byte) fail — the converter-failure
            // branch in Utf8ColumnParserFactory.BuildConverter.
            Assert.False(new Utf8FixedWidthParser<CentsConverterModel>().TryParse("notanum!"u8, Inv, null, out _));
        }

        // ----- UTF-8 culture parser: wide field forces the ArrayPool transcode branch -----

        [Fact]
        public void Utf8_WideNumericField_CommaCulture_UsesTranscodeArrayPoolBranch()
        {
            // Fields longer than the 128-char stack buffer, and a non-'.' separator so the parser takes
            // the transcode path (TryParseDoubleViaTranscode / TryParseFloatViaTranscode) — its long branch.
            string field = ("0," + new string('1', 150)).PadRight(200);
            var bytes = Encoding.UTF8.GetBytes(field + field);

            bool ok = new Utf8FixedWidthParser<WideUtf8NumericModel>().TryParse(bytes, DeDe, null, out var m);

            Assert.True(ok);
            Assert.InRange(m.D, 0.11, 0.12);
            Assert.InRange(m.F, 0.11f, 0.12f);
        }

        // ----- Converter formatter: ArrayPool growth loop and exhaustion -----

        [Fact]
        public void FormatConvert_OutputLargerThanStackBuffer_UsesArrayPoolGrowth()
        {
            var options = new ColumnFormatOptions(Alignment.Left, ' ', null, OverflowBehavior.Truncate);
            Span<char> slice = stackalloc char[200];

            // 100 chars > the 64-char stack buffer → the ArrayPool growth loop writes it instead.
            FixedWidthRuntime.FormatConvert(new BigValue(100), slice, Inv, new BigConverter(), options, "Col");

            Assert.Equal(new string('x', 100) + new string(' ', 100), slice.ToString());
        }

        [Fact]
        public void FormatConvert_OutputNeverFits_Throws()
        {
            var ex = Record.Exception(FormatConvertThatNeverFits);
            var iop = Assert.IsType<InvalidOperationException>(ex);
            Assert.Contains("Col", iop.Message, StringComparison.Ordinal);
        }

        // Separate method: a Span<char> can't be captured by the Record.Exception lambda.
        private static void FormatConvertThatNeverFits()
        {
            var options = new ColumnFormatOptions(Alignment.Left, ' ', null, OverflowBehavior.Truncate);
            Span<char> slice = stackalloc char[8]; // maxSize = max(1024, 8*16) = 1024
            FixedWidthRuntime.FormatConvert(new BigValue(2000), slice, Inv, new BigConverter(), options, "Col");
        }
    }
}
