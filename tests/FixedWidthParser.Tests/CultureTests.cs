using System.Globalization;
using FixedWidthParser.Parsers;
using FixedWidthParser.Writers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Culture handling. The format provider comes from <see cref="FixedWidthContext"/>; the default
    /// context means the invariant culture for every column type, on parse and on write.
    /// </summary>
    public class CultureTests
    {
        private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");
        private static readonly FixedWidthContext German = CtxWith(DeDe);

        [Fact]
        public void Parse_DecimalColumn_HonorsCommaCulture()
        {
            Assert.True(new FixedWidthParser<DecimalModel>().TryParse("1234,56     "u8, German, out var model));
            Assert.Equal(1234.56m, model.Amount);
        }

        [Fact]
        public void Parse_DecimalColumn_DefaultContext_IsInvariant()
        {
            Assert.True(new FixedWidthParser<DecimalModel>().TryParse("1234.56     "u8, Ctx, out var model));
            Assert.Equal(1234.56m, model.Amount);
        }

        [Fact]
        public void Parse_DoubleColumn_HonorsCommaCulture()
        {
            Assert.True(new FixedWidthParser<PersonModel>().TryParse("Bob       30   1234,50   "u8, German, out var model));
            Assert.Equal(1234.50, model.Salary, 2);
        }

        [Fact]
        public void Parse_DoubleColumn_DefaultContext_UsesDot()
        {
            Assert.True(new FixedWidthParser<PersonModel>().TryParse("Bob       30   1234.50   "u8, Ctx, out var model));
            Assert.Equal(1234.50, model.Salary, 2);
        }

        [Fact]
        public void Parse_FloatColumn_HonorsCommaCulture()
        {
            Assert.True(new FixedWidthParser<MeasurementModel>().TryParse("3,14    "u8, German, out var model));
            Assert.Equal(3.14f, model.Value, 2);
        }

        // csFastFloat's single decimal_separator override can't express a thousands separator; the
        // BCL fallback accepts the value and still rejects real garbage.
        [Fact]
        public void Parse_DoubleColumn_DotCultureThousandsSeparator_ParsesFullValue()
        {
            Assert.True(new FixedWidthParser<PersonModel>().TryParse("Bob       30    1,234.50 "u8, Ctx, out var model));
            Assert.Equal(1234.50, model.Salary, 2);
        }

        // Regression: "1.234,50" under de-DE used to come back as 1.0 from csFastFloat.
        [Fact]
        public void Parse_DoubleColumn_DeDeThousandsSeparator_ParsesFullValue()
        {
            Assert.True(new FixedWidthParser<PersonModel>().TryParse("Bob       30   1.234,50  "u8, German, out var model));
            Assert.Equal(1234.50, model.Salary, 2);
        }

        [Fact]
        public void Parse_DoubleColumn_TrailingGarbageAfterNumber_Fails()
        {
            Assert.False(new FixedWidthParser<PersonModel>().TryParse("Bob       30   12x       "u8, Ctx, out _));
        }

        [Fact]
        public void Parse_FloatColumn_TrailingGarbageAfterNumber_Fails()
        {
            Assert.False(new FixedWidthParser<MeasurementModel>().TryParse("12x     "u8, Ctx, out _));
        }

        // csFastFloat's characters_consumed does not consistently include skipped leading whitespace,
        // so right-aligned columns must be trimmed before the fast path.
        [Fact]
        public void Parse_DoubleColumn_RightAligned_DotCulture_Parses()
        {
            Assert.True(new FixedWidthParser<PersonModel>().TryParse("Bob       30       123.45"u8, Ctx, out var model));
            Assert.Equal(123.45, model.Salary, 2);
        }

        [Fact]
        public void Parse_FloatColumn_RightAligned_DotCulture_Parses()
        {
            Assert.True(new FixedWidthParser<MeasurementModel>().TryParse("    3.14"u8, Ctx, out var model));
            Assert.Equal(3.14f, model.Value, 2);
        }

        [Fact]
        public void Parse_DoubleColumn_RightAligned_CommaCulture_Parses()
        {
            Assert.True(new FixedWidthParser<PersonModel>().TryParse("Bob       30       123,45"u8, German, out var model));
            Assert.Equal(123.45, model.Salary, 2);
        }

        [Fact]
        public void Parse_FloatColumn_RightAligned_CommaCulture_Parses()
        {
            Assert.True(new FixedWidthParser<MeasurementModel>().TryParse("    3,14"u8, German, out var model));
            Assert.Equal(3.14f, model.Value, 2);
        }

        // A separator that is not one byte (U+066B in UTF-8) used to be refused; the value is now
        // decoded and parsed by the BCL with the real culture.
        [Fact]
        public void Parse_DoubleColumn_NonAsciiSeparatorCulture_Parses()
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = "٫";

            Assert.True(new FixedWidthParser<PersonModel>().TryParse(Bytes("Bob       30   1234٫50  "), CtxWith(culture), out var model));
            Assert.Equal(1234.50, model.Salary, 2);
        }

        // Regression guard for the old single-entry decimal-separator memo.
        [Fact]
        public void Parse_DoubleColumn_AlternatingProviders_BothStayCorrect()
        {
            var parser = new FixedWidthParser<PersonModel>();

            for (int i = 0; i < 5; i++)
            {
                Assert.True(parser.TryParse("Bob       30   1234.50   "u8, Ctx, out var invariant));
                Assert.Equal(1234.50, invariant.Salary, 2);

                Assert.True(parser.TryParse("Bob       30   1234,50   "u8, German, out var german));
                Assert.Equal(1234.50, german.Salary, 2);
            }
        }

        [Fact]
        public void Write_DoubleColumn_UsesCultureDecimalSeparator()
        {
            var writer = new FixedWidthWriter<PersonModel>(new FixedWidthWriterOptions { FormatProvider = DeDe });

            string line = WriteOne(writer, new PersonModel { Name = "Bob", Age = 30, Salary = 1234.5 });

            Assert.Equal("1234,5", line[15..21]);
        }

        [Fact]
        public void RoundTrip_DoubleColumn_DefaultOptions_PreservesValue()
        {
            var original = new PersonModel { Name = "Bob", Age = 30, Salary = 1234.5 };

            string line = WriteOne(new FixedWidthWriter<PersonModel>(), original);

            Assert.True(new FixedWidthParser<PersonModel>().TryParse(Bytes(line), Ctx, out var parsed));
            Assert.Equal(original.Salary, parsed.Salary, 2);
        }

        [Fact]
        public void RoundTrip_DoubleColumn_UnderCommaCulture_PreservesValue()
        {
            var writer = new FixedWidthWriter<PersonModel>(new FixedWidthWriterOptions { FormatProvider = DeDe });
            var original = new PersonModel { Name = "Bob", Age = 30, Salary = 1234.5 };

            string line = WriteOne(writer, original);

            Assert.True(new FixedWidthParser<PersonModel>().TryParse(Bytes(line), German, out var parsed));
            Assert.Equal(original.Salary, parsed.Salary, 2);
        }
    }
}
