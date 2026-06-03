using System.Globalization;
using FixedWidthParser.Parsers;
using FixedWidthParser.Writers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Cobre o tratamento de cultura. O caminho genérico (ISpanParsable, ex.: decimal) e a
    /// escrita via ISpanFormattable respeitam o IFormatProvider. Os processadores de
    /// double/float derivam o separador decimal da cultura e o repassam ao csFastFloat, de
    /// modo que também passam a respeitar o IFormatProvider (com '.' como padrão quando nulo).
    /// </summary>
    public class CultureTests
    {
        private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

        // ----- Caminho genérico (decimal): respeita a cultura -----

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

        // ----- Caminho double: separador decimal derivado da cultura -----

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

        // ----- Escrita: respeita a cultura (ISpanFormattable) -----

        [Fact]
        public void Write_DoubleColumn_UsesCultureDecimalSeparator()
        {
            var writer = new FixedWidthWriter<PersonModel>();

            string line = WriteOne(writer, new PersonModel { Name = "Bob", Age = 30, Salary = 1234.5 }, DeDe);

            // Coluna Salary em [15,25): formatada com vírgula sob de-DE.
            Assert.Equal("1234,5", line[15..21]);
        }

        // ----- Round-trip sob cultura com vírgula agora preserva o valor -----

        [Fact]
        public void RoundTrip_DoubleColumn_UnderCommaCulture_PreservesValue()
        {
            var writer = new FixedWidthWriter<PersonModel>();
            var parser = new FixedWidthParser<PersonModel>();
            var original = new PersonModel { Name = "Bob", Age = 30, Salary = 1234.5 };

            // Escrita gera "1234,5"; leitura usa o mesmo separador da cultura → mesmo valor.
            string line = WriteOne(writer, original, DeDe);
            bool ok = parser.TryParse(line, DeDe, null, out var parsed);

            Assert.True(ok);
            Assert.Equal(original.Salary, parsed.Salary, 2);
        }
    }
}
