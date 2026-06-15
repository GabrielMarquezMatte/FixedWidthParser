using System.Globalization;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Attributes;
using FixedWidthParser.Parsers;
using FixedWidthParser.Processors;
using FixedWidthParser.Readers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Covers the UTF-8 / byte parse path: <see cref="Utf8FixedWidthParser{TModel}"/> and the
    /// <see cref="FixedWidthByteReader{TModel}"/> facade. Column offsets are measured in bytes;
    /// all fixtures here are ASCII, so byte offsets equal character offsets. Mirrors the assertions
    /// in <see cref="ReaderTests"/>/<see cref="CultureTests"/> to prove byte/char parity.
    /// </summary>
    public class ByteReaderTests
    {
        private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

        // ----------------------- Core parsing -----------------------

        [Fact]
        public void Parser_ParsesAllColumns()
        {
            var parser = new Utf8FixedWidthParser<PersonModel>();

            bool ok = parser.TryParse("John Doe  30   60000.00  "u8, Inv, null, out var model);

            Assert.True(ok);
            Assert.Equal("John Doe", model.Name);
            Assert.Equal(30, model.Age);
            Assert.Equal(60000.00, model.Salary, 2);
        }

        [Fact]
        public void Parser_StringColumn_TrimsTrailingSpacesAndDecodes()
        {
            var parser = new Utf8FixedWidthParser<CodeModel>();

            bool ok = parser.TryParse("ABC"u8, null, null, out var model);

            Assert.True(ok);
            Assert.Equal("ABC", model.Code);
        }

        [Fact]
        public void Parser_DecimalColumn_UsesUtf8SpanParsableFallback()
        {
            var parser = new Utf8FixedWidthParser<DecimalModel>();

            bool ok = parser.TryParse("1234.56     "u8, Inv, null, out var model);

            Assert.True(ok);
            Assert.Equal(1234.56m, model.Amount);
        }

        [Fact]
        public void Parser_FloatColumn_UsesCsFastFloat()
        {
            var parser = new Utf8FixedWidthParser<MeasurementModel>();

            bool ok = parser.TryParse("3.14    "u8, Inv, null, out var model);

            Assert.True(ok);
            Assert.Equal(3.14f, model.Value, 2);
        }

        [Fact]
        public void Parser_FieldBasedModel_Parses()
        {
            var parser = new Utf8FixedWidthParser<ProductModel>();

            bool ok = parser.TryParse("ABCDE0042"u8, Inv, null, out var model);

            Assert.True(ok);
            Assert.Equal("ABCDE", model.Code);
            Assert.Equal(42, model.Quantity);
        }

        [Fact]
        public void Parser_SupportsRefStructModel()
        {
            var parser = new Utf8FixedWidthParser<RefPersonModel>();

            bool ok = parser.TryParse("John Doe  30   60000.00  "u8, Inv, null, out var model);

            Assert.True(ok);
            Assert.Equal("John Doe", model.Name);
            Assert.Equal(30, model.Age);
            Assert.Equal(60000.00, model.Salary, 2);
        }

        // ----------------------- Rejection paths -----------------------

        [Fact]
        public void Parser_LineShorterThanLayout_ReturnsFalse()
        {
            var parser = new Utf8FixedWidthParser<PersonModel>();

            // Layout requires 25 bytes; supply fewer.
            bool ok = parser.TryParse("John Doe  30"u8, Inv, null, out _);

            Assert.False(ok);
        }

        [Fact]
        public void Parser_InvalidNumericColumn_ReturnsFalse()
        {
            var parser = new Utf8FixedWidthParser<PersonModel>();

            // "XX" in the Age column [10,5) is not an integer.
            bool ok = parser.TryParse("John Doe  XX   60000.00  "u8, Inv, null, out _);

            Assert.False(ok);
        }

        // ----------------------- Culture handling -----------------------

        [Fact]
        public void Parser_DoubleColumn_HonorsCommaCulture()
        {
            var parser = new Utf8FixedWidthParser<PersonModel>();

            bool ok = parser.TryParse("Bob       30   1234,50   "u8, DeDe, null, out var model);

            Assert.True(ok);
            Assert.Equal(1234.50, model.Salary, 2);
        }

        [Fact]
        public void Parser_DoubleColumn_NullProvider_DefaultsToDot()
        {
            var parser = new Utf8FixedWidthParser<PersonModel>();

            bool ok = parser.TryParse("Bob       30   1234.50   "u8, null, null, out var model);

            Assert.True(ok);
            Assert.Equal(1234.50, model.Salary, 2);
        }

        [Fact]
        public void Parser_DecimalColumn_HonorsCommaCulture()
        {
            var parser = new Utf8FixedWidthParser<DecimalModel>();

            bool ok = parser.TryParse("1234,56     "u8, DeDe, null, out var model);

            Assert.True(ok);
            Assert.Equal(1234.56m, model.Amount);
        }

        [Fact]
        public void Parser_FloatColumn_HonorsCommaCulture()
        {
            var parser = new Utf8FixedWidthParser<MeasurementModel>();

            bool ok = parser.TryParse("3,14    "u8, DeDe, null, out var model);

            Assert.True(ok);
            Assert.Equal(3.14f, model.Value, 2);
        }

        [Fact]
        public void Parser_DoubleColumn_NonAsciiSeparatorCulture_Throws()
        {
            // The byte parser matches the decimal separator against raw UTF-8 bytes, so a non-ASCII
            // separator (here U+066B ARABIC DECIMAL SEPARATOR) cannot be represented as a single byte.
            // It must be rejected with a clear error rather than silently truncated to the wrong byte.
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = "٫";

            var parser = new Utf8FixedWidthParser<PersonModel>();

            var ex = Assert.Throws<NotSupportedException>(
                () => parser.TryParse("Bob       30   1234.50   "u8, culture, null, out _));
            Assert.Contains("ASCII", ex.Message, StringComparison.Ordinal);
        }

        // ----------------------- Facade -----------------------

        [Fact]
        public void ByteReader_TryParse_UsesConfiguredCulture()
        {
            var reader = new FixedWidthByteReader<PersonModel>(Inv);

            bool ok = reader.TryParse("Jane      28   55000.00  "u8, out var model);

            Assert.True(ok);
            Assert.Equal("Jane", model.Name);
            Assert.Equal(28, model.Age);
            Assert.Equal(55000.00, model.Salary, 2);
        }

        [Fact]
        public void ByteReader_TryParse_CommaCulture()
        {
            var reader = new FixedWidthByteReader<DecimalModel>(DeDe);

            bool ok = reader.TryParse("1234,56     "u8, out var model);

            Assert.True(ok);
            Assert.Equal(1234.56m, model.Amount);
        }

        [Fact]
        public void ByteReader_TryParse_TooShort_ReturnsFalse()
        {
            var reader = new FixedWidthByteReader<PersonModel>(Inv);

            bool ok = reader.TryParse("short"u8, out _);

            Assert.False(ok);
        }

        // ----------------------- char/byte parity -----------------------

        [Fact]
        public void ByteParser_MatchesCharParser_ForAsciiInput()
        {
            const string line = "John Doe  30   60000.00  ";
            var charParser = new FixedWidthParser<PersonModel>();
            var byteParser = new Utf8FixedWidthParser<PersonModel>();

            bool charOk = charParser.TryParse(line, Inv, null, out var fromChars);
            bool byteOk = byteParser.TryParse("John Doe  30   60000.00  "u8, Inv, null, out var fromBytes);

            Assert.True(charOk);
            Assert.True(byteOk);
            Assert.Equal(fromChars.Name, fromBytes.Name);
            Assert.Equal(fromChars.Age, fromBytes.Age);
            Assert.Equal(fromChars.Salary, fromBytes.Salary, 2);
        }

        // ----------------------- StringPool interning -----------------------

        [Fact]
        public void Parser_WithStringPool_InternsRepeatedValues()
        {
            var pool = new StringPool();
            var parser = new Utf8FixedWidthParser<CodeModel>();

            Assert.True(parser.TryParse("ABC"u8, null, pool, out var first));
            Assert.True(parser.TryParse("ABC"u8, null, pool, out var second));

            Assert.Equal("ABC", first.Code);
            Assert.Same(first.Code, second.Code);
        }

        [Fact]
        public void Parser_WithStringPool_MatchesNonPooledDecode()
        {
            var pool = new StringPool();
            var parser = new Utf8FixedWidthParser<CodeModel>();

            Assert.True(parser.TryParse("ABC"u8, null, null, out var nonPooled));
            Assert.True(parser.TryParse("ABC"u8, null, pool, out var pooled));

            Assert.Equal(nonPooled.Code, pooled.Code);
        }

        [Fact]
        public void ByteReader_TryParse_WithStringPool_InternsRepeatedValues()
        {
            var pool = new StringPool();
            var reader = new FixedWidthByteReader<CodeModel>(stringPool: pool);

            Assert.True(reader.TryParse("ABC"u8, out var first));
            Assert.True(reader.TryParse("ABC"u8, out var second));

            Assert.Same(first.Code, second.Code);
        }

        // ----------------------- Registry extensibility -----------------------

        /// <summary>A value type that does NOT implement IUtf8SpanParsable, so it can only be parsed
        /// through a registered <see cref="Utf8ColumnValueParser{TValue}"/>.</summary>
        public readonly record struct Celsius(int Degrees);

        public readonly record struct TempModel
        {
            public TempModel()
            {
                Temp = default;
            }

            [FixedColumn(0, 4)] public Celsius Temp { get; init; }
        }

        [Fact]
        public void Registry_RegisteredParser_IsUsedForCustomType()
        {
            // Must register before the first construction of the parser for this model: the parser
            // caches its column parsers in a static constructor.
            Utf8ColumnParserRegistry.Register<Celsius>(
                static (span, fp, out value) =>
                {
                    if (int.TryParse(span.TrimEnd((byte)' '), fp, out int degrees))
                    {
                        value = new Celsius(degrees);
                        return true;
                    }
                    value = default;
                    return false;
                });
            try
            {
                var parser = new Utf8FixedWidthParser<TempModel>();

                bool ok = parser.TryParse("  25"u8, Inv, null, out var model);

                Assert.True(ok);
                Assert.Equal(new Celsius(25), model.Temp);
            }
            finally
            {
                Assert.True(Utf8ColumnParserRegistry.Unregister<Celsius>());
            }
        }
    }
}
