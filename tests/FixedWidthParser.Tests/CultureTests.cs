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

        // The fast path refuses a thousands separator; the BCL fallback accepts the value and still
        // rejects real garbage.
        [Fact]
        public void Parse_DoubleColumn_DotCultureThousandsSeparator_ParsesFullValue()
        {
            Assert.True(FixedWidth.TryParse("Bob       30    1,234.50 "u8, FixedWidthLayout.FromAttributes<PersonModel>(), Ctx, out var model));
            Assert.Equal(1234.50, model.Salary, 2);
        }

        // Regression: "1.234,50" under de-DE used to come back as 1.0 from a prefix-only fast parse.
        [Fact]
        public void Parse_DoubleColumn_DeDeThousandsSeparator_ParsesFullValue()
        {
            Assert.True(FixedWidth.TryParse("Bob       30   1.234,50  "u8, FixedWidthLayout.FromAttributes<PersonModel>(), German, out var model));
            Assert.Equal(1234.50, model.Salary, 2);
        }

        // The fast path takes any one-ASCII-character separator, so every accepted value must match the BCL's.
        [Theory]
        [InlineData("de-DE")]
        [InlineData("pt-BR")]
        [InlineData("fr-FR")]
        public void Parse_Double_CommaCulture_MatchesTheBcl(string cultureName)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            var context = CtxWith(culture);
            var options = new Infrastructure.ColumnParseOptions(' ', TrimMode.Both, format: null);
            string[] edges = ["-0,5", "+1,5", "1,5e3", "1,5E-3", "1.234,5", "0,1", ",5", "5,", "007,10", "1,7976931348623157E+308", "4,9E-324", "1,2,3", "--1", "1e", "1,5x", "1 000,5"];
            var random = new Random(42);
            var values = edges.Concat(Enumerable.Range(0, 2000).Select(_ => (random.NextDouble() * Math.Pow(10, random.Next(-8, 12)) * (random.Next(2) * 2 - 1)).ToString("R", culture)));

            foreach (string text in values)
            {
                bool expected = double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, culture, out double bcl);
                bool actual = Infrastructure.FixedWidthRuntime.TryDouble(Bytes(text), in context, in options, out double parsed);

                Assert.True(expected == actual, $"'{text}': BCL {expected}, parser {actual}");
                Assert.True(!expected || bcl.Equals(parsed), $"'{text}': BCL {bcl:R}, parser {parsed:R}");
            }
        }

        // The float fast path rounds once in binary32; going through a double could land one ulp off.
        [Theory]
        [InlineData("")]
        [InlineData("de-DE")]
        [InlineData("fr-FR")]
        public void Parse_Float_MatchesTheBcl(string cultureName)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            var context = CtxWith(culture);
            var options = new Infrastructure.ColumnParseOptions(' ', TrimMode.Both, format: null);
            string separator = culture.NumberFormat.NumberDecimalSeparator;
            string[] edges = ["-0", "16777216", "16777217", $"1{separator}5e3", $"0{separator}1", $"3{separator}4028235E+38", "1e", "--1"];
            var random = new Random(42);
            var shortDecimals = Enumerable.Range(0, 2000).Select(_ => (random.Next(-20_000_000, 20_000_000) / Math.Pow(10, random.Next(0, 11))).ToString("0.##########", culture));
            var roundTrips = Enumerable.Range(0, 2000).Select(_ => ((float)(random.NextDouble() * Math.Pow(10, random.Next(-8, 12)))).ToString("R", culture));

            foreach (string text in edges.Concat(shortDecimals).Concat(roundTrips))
            {
                bool expected = float.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, culture, out float bcl);
                bool actual = Infrastructure.FixedWidthRuntime.TryFloat(Bytes(text), in context, in options, out float parsed);

                Assert.True(expected == actual, $"'{text}': BCL {expected}, parser {actual}");
                Assert.True(!expected || bcl.Equals(parsed), $"'{text}': BCL {bcl:R}, parser {parsed:R}");
            }
        }

        // The decimal fast path keeps the scale as written, as the BCL does: 1,50 stays 1,50.
        [Theory]
        [InlineData("")]
        [InlineData("de-DE")]
        [InlineData("fr-FR")]
        public void Parse_Decimal_MatchesTheBcl(string cultureName)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            var context = CtxWith(culture);
            var options = new Infrastructure.ColumnParseOptions(' ', TrimMode.Both, format: null);
            string separator = culture.NumberFormat.NumberDecimalSeparator;
            string[] edges = ["-0", $"1{separator}50", $"{separator}5", $"5{separator}", "9999999999999999999", "99999999999999999999", $"0{separator}0000000000000000001", "1e3", "--1", "+7"];
            var random = new Random(42);
            var values = Enumerable.Range(0, 2000).Select(_ => (random.NextInt64(-1_000_000_000_000, 1_000_000_000_000) / (decimal)Math.Pow(10, random.Next(0, 13))).ToString(culture));

            foreach (string text in edges.Concat(values))
            {
                bool expected = decimal.TryParse(text, NumberStyles.Number, culture, out decimal bcl);
                bool actual = Infrastructure.FixedWidthRuntime.TryParse(Bytes(text), in context, in options, out decimal parsed);

                Assert.True(expected == actual, $"'{text}': BCL {expected}, parser {actual}");
                Assert.True(!expected || bcl.ToString(CultureInfo.InvariantCulture) == parsed.ToString(CultureInfo.InvariantCulture), $"'{text}': BCL {bcl}, parser {parsed}");
            }
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

        // The fast path takes no whitespace, so right-aligned columns must be trimmed before it.
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
