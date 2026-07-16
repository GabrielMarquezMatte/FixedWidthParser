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

            bool ok = parser.TryParse("1234,56     ", DeDe, null, out var model);

            Assert.True(ok);
            Assert.Equal(1234.56m, model.Amount);
        }

        [Fact]
        public void Parse_DecimalColumn_HonorsDotCulture()
        {
            var parser = new FixedWidthParser<DecimalModel>();

            bool ok = parser.TryParse("1234.56     ", CultureInfo.InvariantCulture, null, out var model);

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

        // ----- Null provider means invariant for EVERY column type, not just double/float: before this
        // fix, ISpanParsable columns (decimal, int, DateTime, …) fell through to the BCL's own default
        // of CurrentCulture on a null provider, while double/float were hard-coded to invariant — so the
        // same line could parse two different ways for two different column types under one null-provider
        // call. Pinning both to invariant also makes this test's outcome independent of whatever culture
        // happens to be ambient when the suite runs. -----

        [Fact]
        public void Parse_DecimalColumn_NullProvider_DefaultsToInvariant()
        {
            var parser = new FixedWidthParser<DecimalModel>();

            bool ok = parser.TryParse("1234.56     ", null, null, out var model);

            Assert.True(ok);
            Assert.Equal(1234.56m, model.Amount);
        }

        // ----- Round-trip under the default (null) provider must preserve the value regardless of the
        // machine's ambient culture — before this fix, write (CurrentCulture via ISpanFormattable.TryFormat)
        // and parse (invariant via CultureHelpers) disagreed on what null means, so a comma-decimal machine
        // would write "1234,5" and then fail to parse its own output back with the same null provider. -----

        [Fact]
        public void RoundTrip_DoubleColumn_NullProvider_PreservesValue()
        {
            var writer = new FixedWidthWriter<PersonModel>();
            var parser = new FixedWidthParser<PersonModel>();
            var original = new PersonModel { Name = "Bob", Age = 30, Salary = 1234.5 };

            string line = WriteOne(writer, original);
            bool ok = parser.TryParse(line, null, null, out var parsed);

            Assert.True(ok);
            Assert.Equal(original.Salary, parsed.Salary, 2);
        }

        // ----- Thousands separators under the invariant '.' fast path: csFastFloat's single
        // decimal_separator override can't express a thousands separator, so it used to fail the
        // full-consumption check and reject the whole line even though the value is valid; the BCL
        // fallback now retried on that failure accepts it (and still rejects real garbage, see
        // Parse_DoubleColumn_TrailingGarbageAfterNumber_Fails above). -----

        [Fact]
        public void Parse_DoubleColumn_DotCultureThousandsSeparator_ParsesFullValue()
        {
            var parser = new FixedWidthParser<PersonModel>();

            bool ok = parser.TryParse("Bob       30    1,234.50 ", Inv, null, out var model);

            Assert.True(ok);
            Assert.Equal(1234.50, model.Salary, 2);
        }

        // ----- Thousands separators: csFastFloat's decimal_separator override silently truncates at
        // the first character it doesn't recognize (e.g. a thousands separator) instead of failing
        // only real NumberFormatInfo-aware parsing (used whenever the separator isn't '.') gets this
        // right. Regression coverage for a bug where "1.234,50" under de-DE silently became 1.0. -----

        [Fact]
        public void Parse_DoubleColumn_DeDeThousandsSeparator_ParsesFullValue()
        {
            var parser = new FixedWidthParser<PersonModel>();

            // de-DE: '.' groups thousands, ',' is the decimal separator — "1.234,50" means 1234.50.
            bool ok = parser.TryParse("Bob       30   1.234,50  ", DeDe, null, out var model);

            Assert.True(ok);
            Assert.Equal(1234.50, model.Salary, 2);
        }

        // ----- Trailing garbage after a dot-separated number: the fast path must reject it instead of
        // silently truncating to the leading digits and reporting success. -----

        [Fact]
        public void Parse_DoubleColumn_TrailingGarbageAfterNumber_Fails()
        {
            var parser = new FixedWidthParser<PersonModel>();

            bool ok = parser.TryParse("Bob       30   12x       ", Inv, null, out _);

            Assert.False(ok);
        }

        [Fact]
        public void Parse_FloatColumn_TrailingGarbageAfterNumber_Fails()
        {
            var parser = new FixedWidthParser<MeasurementModel>();

            bool ok = parser.TryParse("12x     ", Inv, null, out _);

            Assert.False(ok);
        }

        // ----- Right-aligned (leading-space-padded) numeric columns: csFastFloat's characters_consumed
        // does not consistently include skipped leading whitespace across its double/float overloads
        // (FastDoubleParser counted it, FastFloatParser didn't, and vice versa on the UTF-8 overloads),
        // so a right-aligned column could fail the full-consumption check even under the invariant '.'
        // fast path — the exact opposite of a version that should always accept it. -----

        [Fact]
        public void Parse_DoubleColumn_RightAligned_DotCulture_Parses()
        {
            var parser = new FixedWidthParser<PersonModel>();

            bool ok = parser.TryParse("Bob       30       123.45", Inv, null, out var model);

            Assert.True(ok);
            Assert.Equal(123.45, model.Salary, 2);
        }

        [Fact]
        public void Parse_FloatColumn_RightAligned_DotCulture_Parses()
        {
            var parser = new FixedWidthParser<MeasurementModel>();

            bool ok = parser.TryParse("    3.14", Inv, null, out var model);

            Assert.True(ok);
            Assert.Equal(3.14f, model.Value, 2);
        }

        [Fact]
        public void Parse_DoubleColumn_RightAligned_CommaCulture_Parses()
        {
            var parser = new FixedWidthParser<PersonModel>();

            bool ok = parser.TryParse("Bob       30       123,45", DeDe, null, out var model);

            Assert.True(ok);
            Assert.Equal(123.45, model.Salary, 2);
        }

        [Fact]
        public void Parse_FloatColumn_RightAligned_CommaCulture_Parses()
        {
            var parser = new FixedWidthParser<MeasurementModel>();

            bool ok = parser.TryParse("    3,14", DeDe, null, out var model);

            Assert.True(ok);
            Assert.Equal(3.14f, model.Value, 2);
        }

        // ----- Regression guard for the old single-entry decimal-separator memo: alternating between
        // two providers must not thrash into wrong results for either one. -----

        [Fact]
        public void Parse_DoubleColumn_AlternatingProviders_BothStayCorrect()
        {
            var parser = new FixedWidthParser<PersonModel>();

            for (int i = 0; i < 5; i++)
            {
                Assert.True(parser.TryParse("Bob       30   1234.50   ", Inv, null, out var invModel));
                Assert.Equal(1234.50, invModel.Salary, 2);

                Assert.True(parser.TryParse("Bob       30   1234,50   ", DeDe, null, out var deModel));
                Assert.Equal(1234.50, deModel.Salary, 2);
            }
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
