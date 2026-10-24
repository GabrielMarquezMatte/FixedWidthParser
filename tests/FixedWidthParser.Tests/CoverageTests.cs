using System.Buffers;
using System.Globalization;
using System.Text;
using FixedWidthParser.Infrastructure;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Branches the type-focused suites leave out: the TimeOnly/DateTimeOffset exact parsers, the
    /// decode fallback for wide numeric fields, and the converter formatter's ArrayPool growth/exhaustion.
    /// </summary>
    public class CoverageTests
    {
        [Fact]
        public void TimeExact_RoundTrip_BothPaths()
        {
            var time = new TimeOnly(13, 45, 30);
            var timestamp = new DateTimeOffset(2026, 7, 16, 13, 45, 30, TimeSpan.FromHours(3));
            byte[] input = Bytes("13453020260716134530+03:00");

            Assert.True(FixedWidth.TryParse(input, FixedWidthLayout.FromAttributes<TimeExactModel>(), Ctx, out var reflected));
            Assert.Equal(time, reflected.Time);
            Assert.Equal(timestamp, reflected.Timestamp);

            Assert.True(FixedWidth.TryParse<GenTimeExactModel>(input, Ctx, out var generated));
            Assert.Equal(time, generated.Time);
            Assert.Equal(timestamp, generated.Timestamp);
        }

        [Fact]
        public void TimeExact_InvalidField_ReturnsFalse()
        {
            byte[] bad = Bytes("xxxxxx20260716134530+03:00");

            Assert.False(FixedWidth.TryParse(bad, FixedWidthLayout.FromAttributes<TimeExactModel>(), Ctx, out _));
            Assert.False(FixedWidth.TryParse<GenTimeExactModel>(bad, Ctx, out _));
        }

        [Fact]
        public void StrictDecoding_InvalidWideColumn_ReturnsTheRentedBuffer()
        {
            // Wider than the 256-char stack buffer, so decoding rents; the trailing 0xFF makes the strict decoder throw.
            var strict = new UTF8Encoding(false, true);
            byte[] column = new byte[300];
            column.AsSpan().Fill((byte)'1');
            column[^1] = 0xFF;
            var options = new ColumnParseOptions(' ', TrimMode.Trailing, "yyyyMMdd");
            int size = strict.GetMaxCharCount(column.Length);

            // The shared pool hands a thread back the array it last returned, so a lost rental shows as a different array.
            char[] cached = ArrayPool<char>.Shared.Rent(size);
            ArrayPool<char>.Shared.Return(cached);

            Assert.Throws<DecoderFallbackException>(() => FixedWidthRuntime.TryDateOnlyExact(column, CtxWith(encoding: strict), in options, out _));

            char[] next = ArrayPool<char>.Shared.Rent(size);
            ArrayPool<char>.Shared.Return(next);
            Assert.Same(cached, next);
        }

        [Fact]
        public void WideNumericField_CommaCulture_DecodesThroughArrayPool()
        {
            // Wider than the 128-char stack buffer, with a ',' separator so the BCL fallback runs.
            string field = ("0," + new string('1', 150)).PadRight(200);

            bool ok = FixedWidth.TryParse(Bytes(field + field), FixedWidthLayout.FromAttributes<WideUtf8NumericModel>(), CtxWith(CultureInfo.GetCultureInfo("de-DE")), out var m);

            Assert.True(ok);
            Assert.InRange(m.D, 0.11, 0.12);
            Assert.InRange(m.F, 0.11f, 0.12f);
        }

        [Fact]
        public void FormatConvert_OutputLargerThanStackBuffer_UsesArrayPoolGrowth()
        {
            var options = new ColumnFormatOptions(Alignment.Left, ' ', null, OverflowBehavior.Truncate);
            byte[] slice = new byte[400];

            // 300 bytes > the 256-byte stack buffer, so the ArrayPool growth loop writes it instead.
            FixedWidthRuntime.FormatConvert(new BigValue(300), slice, Ctx, new BigConverter(), options, "Col");

            Assert.Equal(new string('x', 300) + new string(' ', 100), Encoding.ASCII.GetString(slice));
        }

        [Fact]
        public void FormatConvert_OutputNeverFits_Throws()
        {
            var options = new ColumnFormatOptions(Alignment.Left, ' ', null, OverflowBehavior.Truncate);
            byte[] slice = new byte[8];

            var ex = Assert.Throws<InvalidOperationException>(
                () => FixedWidthRuntime.FormatConvert(new BigValue(10_000), slice, Ctx, new BigConverter(), options, "Col"));

            Assert.Contains("Col", ex.Message, StringComparison.Ordinal);
        }
    }
}
