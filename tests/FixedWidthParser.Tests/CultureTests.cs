using System.Globalization;
using FixedWidthParser.Parsers;
using FixedWidthParser.Writers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Covers culture handling. The generic path (ISpanParsable, e.g. decimal) and writing via
    /// ISpanFormattable honor the IFormatProvider. The double/float processors derive the decimal
    /// separator from the culture and pass it to csFastFloat, so they also honor the
    /// IFormatProvider (with '.' as the default when null).
    /// </summary>
    public class CultureTests
    {
        private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

        // ----- Generic path (decimal): honors the culture -----

        [Fact]
        public void Parse_DecimalColumn_HonorsCommaCulture()
        {
            var parser = new FixedWidthParser<DecimalModel>();

            bool ok = parser.TryParse("1234,56", DeDe, null, out var model);

            Assert.True(ok);
            Assert.Equal(1234.56m, model.Amount);
        }

        [Fact]
        public void Parse_DecimalColumn_HonorsDotCulture()
        {
            var parser = new FixedWidthParser<DecimalModel>();

            bool ok = parser.TryParse("1234.56", CultureInfo.InvariantCulture, null, out var model);

            Assert.True(ok);
            Assert.Equal(1234.56m, model.Amount);
        }

        // ----- Double path: decimal separator derived from the culture -----

        [Fact]
        public void Parse_DoubleColumn_HonorsCommaCulture()
        {
            var parser = new FixedWidthParser<PersonModel>();

            bool ok = parser.TryParse("Bob       30   1234,50   ", DeDe, null, out var model);

            Assert.True(ok);
            Assert.Equal(1234.50, model.Salary, 2);
        }

        [Fact]
        public void Parse_DoubleColumn_HonorsDotCulture()
        {
            var parser = new FixedWidthParser<PersonModel>();

            bool ok = parser.TryParse("Bob       30   1234.50   ", CultureInfo.InvariantCulture, null, out var model);

            Assert.True(ok);
            Assert.Equal(1234.50, model.Salary, 2);
        }

        [Fact]
        public void Parse_DoubleColumn_NullProvider_DefaultsToDot()
        {
            var parser = new FixedWidthParser<PersonModel>();

            bool ok = parser.TryParse("Bob       30   1234.50   ", null, null, out var model);

            Assert.True(ok);
            Assert.Equal(1234.50, model.Salary, 2);
        }

        [Fact]
        public void Parse_FloatColumn_HonorsCommaCulture()
        {
            var parser = new FixedWidthParser<MeasurementModel>();

            bool ok = parser.TryParse("3,14    ", DeDe, null, out var model);

            Assert.True(ok);
            Assert.Equal(3.14f, model.Value, 2);
        }

        // ----- Writing: honors the culture (ISpanFormattable) -----

        [Fact]
        public void Write_DoubleColumn_UsesCultureDecimalSeparator()
        {
            var writer = new FixedWidthWriter<PersonModel>();

            string line = WriteOne(writer, new PersonModel { Name = "Bob", Age = 30, Salary = 1234.5 }, DeDe);

            // Salary column at [15,25): formatted with a comma under de-DE.
            Assert.Equal("1234,5", line[15..21]);
        }

        // ----- Round-trip under a comma culture now preserves the value -----

        [Fact]
        public void RoundTrip_DoubleColumn_UnderCommaCulture_PreservesValue()
        {
            var writer = new FixedWidthWriter<PersonModel>();
            var parser = new FixedWidthParser<PersonModel>();
            var original = new PersonModel { Name = "Bob", Age = 30, Salary = 1234.5 };

            // Writing produces "1234,5"; reading uses the same culture separator → same value.
            string line = WriteOne(writer, original, DeDe);
            bool ok = parser.TryParse(line, DeDe, null, out var parsed);

            Assert.True(ok);
            Assert.Equal(original.Salary, parsed.Salary, 2);
        }
    }
}
