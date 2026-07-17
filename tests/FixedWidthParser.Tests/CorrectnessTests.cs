using System.Text;
using FixedWidthParser.Parsers;
using FixedWidthParser.Writers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    public class CorrectnessTests
    {
        [Fact]
        public void DateTimeExact_RoundTrip_AllPaths()
        {
            var date = new DateTime(2026, 7, 16, 0, 0, 0, DateTimeKind.Unspecified);
            var dateOnly = new DateOnly(2026, 7, 16);
            const string input = "2026071620260716";

            // 1. Reflection Char
            var pChar = new FixedWidthParser<DateTimeExactModel>();
            Assert.True(pChar.TryParse(input, Inv, null, out var model1));
            Assert.Equal(date, model1.Date);
            Assert.Equal(dateOnly, model1.DateOnlyVal);

            var wChar = new FixedWidthWriter<DateTimeExactModel>();
            using (var ms = new MemoryStream())
            using (var sw = new StreamWriter(ms, new UTF8Encoding(false), leaveOpen: true))
            {
                wChar.Write(sw, in model1, Inv);
                sw.Flush();
                Assert.Equal(input + Environment.NewLine, Encoding.UTF8.GetString(ms.ToArray()));
            }

            // 2. Reflection Utf8
            var pUtf8 = new Utf8FixedWidthParser<DateTimeExactModel>();
            var bytes = "2026071620260716"u8;
            Assert.True(pUtf8.TryParse(bytes, Inv, null, out var model2));
            Assert.Equal(date, model2.Date);
            Assert.Equal(dateOnly, model2.DateOnlyVal);

            // 3. Generated Char
            Assert.True(FixedWidth.TryParse<GenDateTimeExactModel>(input, Inv, null, out var model3));
            Assert.Equal(date, model3.Date);
            Assert.Equal(dateOnly, model3.DateOnlyVal);

            Span<char> charDest = stackalloc char[16];
            Assert.True(FixedWidth.TryFormat(model3, charDest, Inv, out int cw));
            Assert.Equal(16, cw);
            Assert.Equal(input, charDest.ToString());

            // 4. Generated Utf8
            Assert.True(FixedWidthUtf8.TryParse<GenDateTimeExactModel>(bytes, Inv, null, out var model4));
            Assert.Equal(date, model4.Date);
            Assert.Equal(dateOnly, model4.DateOnlyVal);
        }

        [Fact]
        public void ZeroPadded_Leading_TrimMode()
        {
            const string input = "0004203.14";
            var bytes = "0004203.14"u8;

            // 1. Reflection Char
            var pChar = new FixedWidthParser<ZeroPaddedLeadingModel>();
            Assert.True(pChar.TryParse(input, Inv, null, out var model1));
            Assert.Equal(42, model1.Value);
            Assert.Equal(3.14, model1.DoubleValue);

            // 2. Reflection Utf8
            var pUtf8 = new Utf8FixedWidthParser<ZeroPaddedLeadingModel>();
            Assert.True(pUtf8.TryParse(bytes, Inv, null, out var model2));
            Assert.Equal(42, model2.Value);
            Assert.Equal(3.14, model2.DoubleValue);

            // 3. Generated Char
            Assert.True(FixedWidth.TryParse<GenZeroPaddedLeadingModel>(input, Inv, null, out var model3));
            Assert.Equal(42, model3.Value);
            Assert.Equal(3.14, model3.DoubleValue);

            // 4. Generated Utf8
            Assert.True(FixedWidthUtf8.TryParse<GenZeroPaddedLeadingModel>(bytes, Inv, null, out var model4));
            Assert.Equal(42, model4.Value);
            Assert.Equal(3.14, model4.DoubleValue);
        }

        [Fact]
        public void ZeroPadded_Both_TrimMode()
        {
            const string input = "00420";
            // 1. Reflection Char
            var pChar = new FixedWidthParser<ZeroPaddedBothModel>();
            Assert.True(pChar.TryParse(input, Inv, null, out var model1));
            Assert.Equal(42, model1.Value);

            // 2. Generated Char
            Assert.True(FixedWidth.TryParse<GenZeroPaddedBothModel>(input, Inv, null, out var model3));
            Assert.Equal(42, model3.Value);
        }

        [Fact]
        public void Nullable_BlankDetection_WithCustomTrimChar()
        {
            // Nullable model with custom TrimChar: should detect spaces or custom TrimChar as null
            const string inputSpace = "          ";
            const string inputZeroAsterisk = "00000*****";

            // 1. Reflection Char
            var pChar = new FixedWidthParser<NullablePaddingModel>();
            Assert.True(pChar.TryParse(inputSpace, Inv, null, out var model1));
            Assert.Null(model1.Value);
            Assert.Null(model1.DoubleValue);

            Assert.True(pChar.TryParse(inputZeroAsterisk, Inv, null, out var model2));
            Assert.Null(model2.Value);
            Assert.Null(model2.DoubleValue);

            var wChar = new FixedWidthWriter<NullablePaddingModel>();
            using (var ms = new MemoryStream())
            using (var sw = new StreamWriter(ms, new UTF8Encoding(false), leaveOpen: true))
            {
                wChar.Write(sw, in model2, Inv);
                sw.Flush();
                Assert.Equal(inputZeroAsterisk + Environment.NewLine, Encoding.UTF8.GetString(ms.ToArray()));
            }

            // 2. Generated Char
            Assert.True(FixedWidth.TryParse<GenNullablePaddingModel>(inputSpace, Inv, null, out var model3));
            Assert.Null(model3.Value);
            Assert.Null(model3.DoubleValue);

            Assert.True(FixedWidth.TryParse<GenNullablePaddingModel>(inputZeroAsterisk, Inv, null, out var model4));
            Assert.Null(model4.Value);
            Assert.Null(model4.DoubleValue);

            Span<char> charDest = stackalloc char[10];
            Assert.True(FixedWidth.TryFormat(model4, charDest, Inv, out int cw));
            Assert.Equal(10, cw);
            Assert.Equal(inputZeroAsterisk, charDest.ToString());
        }

        [Fact]
        public void SignAware_ZeroPadding()
        {
            var modelNegative = new SignPaddedModel { Value = -42 };
            var modelPositive = new SignPaddedModel { Value = 42 };

            var writer = new FixedWidthWriter<SignPaddedModel>();
            using (var ms = new MemoryStream())
            using (var sw = new StreamWriter(ms, new UTF8Encoding(false), leaveOpen: true))
            {
                writer.Write(sw, in modelNegative, Inv);
                sw.Flush();
                Assert.Equal("-00042" + Environment.NewLine, Encoding.UTF8.GetString(ms.ToArray()));
            }

            using (var ms = new MemoryStream())
            using (var sw = new StreamWriter(ms, new UTF8Encoding(false), leaveOpen: true))
            {
                writer.Write(sw, in modelPositive, Inv);
                sw.Flush();
                Assert.Equal("000042" + Environment.NewLine, Encoding.UTF8.GetString(ms.ToArray())); // Default int doesn't format with +, so just zero pad
            }
        }

        [Fact]
        public void Writer_RejectsTruncateOnNonString()
        {
            Assert.Throws<InvalidOperationException>(() => new FixedWidthWriter<TruncateInvalidModel>());
        }
    }
}
