using System.Globalization;
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
            Assert.True(FixedWidth.TryParse("1234,56     "u8, FixedWidthLayout.FromAttributes<DecimalModel>(), German, out var model));
            Assert.Equal(1234.56m, model.Amount);
        }

        [Fact]
        public void Parse_DecimalColumn_DefaultContext_IsInvariant()
        {
            Assert.True(FixedWidth.TryParse("1234.56     "u8, FixedWidthLayout.FromAttributes<DecimalModel>(), Ctx, out var model));
            Assert.Equal(1234.56m, model.Amount);
        }

        [Fact]
        public void Parse_DoubleColumn_HonorsCommaCulture()
        {
            Assert.True(FixedWidth.TryParse("Bob       30   1234,50   "u8, FixedWidthLayout.FromAttributes<PersonModel>(), German, out var model));
            Assert.Equal(1234.50, model.Salary, 2);
        }

        [Fact]
        public void Parse_DoubleColumn_DefaultContext_UsesDot()
        {
            Assert.True(FixedWidth.TryParse("Bob       30   1234.50   "u8, FixedWidthLayout.FromAttributes<PersonModel>(), Ctx, out var model));
            Assert.Equal(1234.50, model.Salary, 2);
        }

        [Fact]
        public void Parse_FloatColumn_HonorsCommaCulture()
        {
            Assert.True(FixedWidth.TryParse("3,14    "u8, FixedWidthLayout.FromAttributes<MeasurementModel>(), German, out var model));
            Assert.Equal(3.14f, model.Value, 2);
        }

        // csFastFloat's single decimal_separator override can't express a thousands separator; the
        // BCL fallback accepts the value and still rejects real garbage.
        [Fact]
        public void Parse_DoubleColumn_DotCultureThousandsSeparator_ParsesFullValue()
        {
            Assert.True(FixedWidth.TryParse("Bob       30    1,234.50 "u8, FixedWidthLayout.FromAttributes<PersonModel>(), Ctx, out var model));
            Assert.Equal(1234.50, model.Salary, 2);
        }

        // Regression: "1.234,50" under de-DE used to come back as 1.0 from csFastFloat.
        [Fact]
        public void Parse_DoubleColumn_DeDeThousandsSeparator_ParsesFullValue()
        {
            Assert.True(FixedWidth.TryParse("Bob       30   1.234,50  "u8, FixedWidthLayout.FromAttributes<PersonModel>(), German, out var model));
            Assert.Equal(1234.50, model.Salary, 2);
        }

        [Fact]
        public void Parse_DoubleColumn_TrailingGarbageAfterNumber_Fails()
        {
            Assert.False(FixedWidth.TryParse("Bob       30   12x       "u8, FixedWidthLayout.FromAttributes<PersonModel>(), Ctx, out _));
        }

        [Fact]
        public void Parse_FloatColumn_TrailingGarbageAfterNumber_Fails()
        {
            Assert.False(FixedWidth.TryParse("12x     "u8, FixedWidthLayout.FromAttributes<MeasurementModel>(), Ctx, out _));
        }

        // csFastFloat's characters_consumed does not consistently include skipped leading whitespace,
        // so right-aligned columns must be trimmed before the fast path.
        [Fact]
        public void Parse_DoubleColumn_RightAligned_DotCulture_Parses()
        {
            Assert.True(FixedWidth.TryParse("Bob       30       123.45"u8, FixedWidthLayout.FromAttributes<PersonModel>(), Ctx, out var model));
            Assert.Equal(123.45, model.Salary, 2);
        }

        [Fact]
        public void Parse_FloatColumn_RightAligned_DotCulture_Parses()
        {
            Assert.True(FixedWidth.TryParse("    3.14"u8, FixedWidthLayout.FromAttributes<MeasurementModel>(), Ctx, out var model));
            Assert.Equal(3.14f, model.Value, 2);
        }

        [Fact]
        public void Parse_DoubleColumn_RightAligned_CommaCulture_Parses()
        {
            Assert.True(FixedWidth.TryParse("Bob       30       123,45"u8, FixedWidthLayout.FromAttributes<PersonModel>(), German, out var model));
            Assert.Equal(123.45, model.Salary, 2);
        }

        [Fact]
        public void Parse_FloatColumn_RightAligned_CommaCulture_Parses()
        {
            Assert.True(FixedWidth.TryParse("    3,14"u8, FixedWidthLayout.FromAttributes<MeasurementModel>(), German, out var model));
            Assert.Equal(3.14f, model.Value, 2);
        }

        // A separator that is not one byte (U+066B in UTF-8) used to be refused; the value is now
        // decoded and parsed by the BCL with the real culture.
        [Fact]
        public void Parse_DoubleColumn_NonAsciiSeparatorCulture_Parses()
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = "٫";

            Assert.True(FixedWidth.TryParse(Bytes("Bob       30   1234٫50  "), FixedWidthLayout.FromAttributes<PersonModel>(), CtxWith(culture), out var model));
            Assert.Equal(1234.50, model.Salary, 2);
        }

        // Regression guard for the old single-entry decimal-separator memo.
        [Fact]
        public void Parse_DoubleColumn_AlternatingProviders_BothStayCorrect()
        {
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();

            for (int i = 0; i < 5; i++)
            {
                Assert.True(FixedWidth.TryParse("Bob       30   1234.50   "u8, layout, Ctx, out var invariant));
                Assert.Equal(1234.50, invariant.Salary, 2);

                Assert.True(FixedWidth.TryParse("Bob       30   1234,50   "u8, layout, German, out var german));
                Assert.Equal(1234.50, german.Salary, 2);
            }
        }

        [Fact]
        public void Write_DoubleColumn_UsesCultureDecimalSeparator()
        {
            var writerOptions = new FixedWidthWriterOptions { FormatProvider = DeDe };
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();

            string line = WriteOne(layout, new PersonModel { Name = "Bob", Age = 30, Salary = 1234.5 }, writerOptions);

            Assert.Equal("1234,5", line[15..21]);
        }

        [Fact]
        public void RoundTrip_DoubleColumn_DefaultOptions_PreservesValue()
        {
            var original = new PersonModel { Name = "Bob", Age = 30, Salary = 1234.5 };

            string line = WriteOne(FixedWidthLayout.FromAttributes<PersonModel>(), original);

            Assert.True(FixedWidth.TryParse(Bytes(line), FixedWidthLayout.FromAttributes<PersonModel>(), Ctx, out var parsed));
            Assert.Equal(original.Salary, parsed.Salary, 2);
        }

        [Fact]
        public void RoundTrip_DoubleColumn_UnderCommaCulture_PreservesValue()
        {
            var writerOptions = new FixedWidthWriterOptions { FormatProvider = DeDe };
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();
            var original = new PersonModel { Name = "Bob", Age = 30, Salary = 1234.5 };

            string line = WriteOne(layout, original, writerOptions);

            Assert.True(FixedWidth.TryParse(Bytes(line), FixedWidthLayout.FromAttributes<PersonModel>(), German, out var parsed));
            Assert.Equal(original.Salary, parsed.Salary, 2);
        }
    }
}
