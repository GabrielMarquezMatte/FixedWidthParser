using System.Globalization;
using System.Text;
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

        // The fast paths read '+' as the positive sign, so a provider with another one must go to the BCL.
        [Fact]
        public void Parse_Numbers_CustomPositiveSign_MatchTheBcl()
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.PositiveSign = "p";
            var context = CtxWith(culture);
            var options = new Infrastructure.ColumnParseOptions(' ', TrimMode.Both, format: null);

            foreach (string text in new[] { "+1", "p1", "-1", "1.5", "+1.5", "p1.5" })
            {
                bool doubleOk = Infrastructure.FixedWidthRuntime.TryDouble(Bytes(text), in context, in options, out double d);
                bool floatOk = Infrastructure.FixedWidthRuntime.TryFloat(Bytes(text), in context, in options, out float f);
                bool decimalOk = Infrastructure.FixedWidthRuntime.TryParse(Bytes(text), in context, in options, out decimal m);

                Assert.Equal(double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, culture, out double bclDouble), doubleOk);
                Assert.Equal(float.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, culture, out float bclFloat), floatOk);
                Assert.Equal(decimal.TryParse(text, NumberStyles.Number, culture, out decimal bclDecimal), decimalOk);
                Assert.Equal((bclDouble, bclFloat, bclDecimal), (d, f, m));
            }
            Assert.False(Infrastructure.FixedWidthRuntime.TryDouble(Bytes("+1"), in context, in options, out _));
        }

        // The digit-only fast path for exact formats must agree with TryParseExact, including where it must
        // step aside: a Buddhist calendar (th-TH) and "/" or ":" standing for other separators (de-DE).
        [Theory]
        [InlineData("")]
        [InlineData("pt-BR")]
        [InlineData("de-DE")]
        [InlineData("th-TH")]
        public void Parse_ExactDates_MatchTheBcl(string cultureName)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            var context = CtxWith(culture);
            string[] formats = ["yyyyMMdd", "yyyy-MM-dd", "dd/MM/yyyy", "yyyyMMddHHmmss", "yyyy-MM-ddTHH:mm:ss", "HHmmss", "HH:mm:ss", "HH", "MMdd", "yyyyMM", "yyyy.MM.dd"];
            string[] edges = ["00000101", "20230230", "20240229", "20230229", "20231301", "99991231", "2023010", "2023o105", "-0230105", "24:00:00", "23:59:60", "235959", "000000", "2023-01-05", "05/01/2023", "05.01.2023"];
            var random = new Random(42);

            foreach (string format in formats)
            {
                var options = new Infrastructure.ColumnParseOptions(' ', TrimMode.Both, format);
                var samples = Enumerable.Range(0, 300)
                    .Select(_ => new DateTime(1, 1, 1).AddSeconds(random.NextInt64(0, (long)(DateTime.MaxValue - DateTime.MinValue).TotalSeconds)))
                    .SelectMany(d => new[] { d.ToString(format, CultureInfo.InvariantCulture), d.ToString(format, culture) });
                foreach (string text in edges.Concat(samples))
                {
                    byte[] bytes = Bytes(text);
                    bool expected = DateTime.TryParseExact(text, format, culture, DateTimeStyles.None, out var bclDateTime);
                    Assert.True(expected == Infrastructure.FixedWidthRuntime.TryDateTimeExact(bytes, in context, in options, out var dateTime) && (!expected || bclDateTime == dateTime && bclDateTime.Kind == dateTime.Kind),
                        $"DateTime '{text}' as {format}: BCL {expected} {bclDateTime:O}, parser {dateTime:O}");
                    expected = DateOnly.TryParseExact(text, format, culture, DateTimeStyles.None, out var bclDate);
                    Assert.True(expected == Infrastructure.FixedWidthRuntime.TryDateOnlyExact(bytes, in context, in options, out var date) && (!expected || bclDate == date),
                        $"DateOnly '{text}' as {format}: BCL {expected} {bclDate:O}, parser {date:O}");
                    expected = TimeOnly.TryParseExact(text, format, culture, DateTimeStyles.None, out var bclTime);
                    Assert.True(expected == Infrastructure.FixedWidthRuntime.TryTimeOnlyExact(bytes, in context, in options, out var time) && (!expected || bclTime == time),
                        $"TimeOnly '{text}' as {format}: BCL {expected} {bclTime:O}, parser {time:O}");
                }
            }
        }

        // sv-SE's negative sign is U+2212, so a leading "-" must not take the integer fast path there.
        [Theory]
        [InlineData("")]
        [InlineData("de-DE")]
        [InlineData("sv-SE")]
        public void Parse_Integers_MatchTheBcl(string cultureName)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            var context = CtxWith(culture);
            var options = new Infrastructure.ColumnParseOptions(' ', TrimMode.Trailing, format: null);
            string[] edges = ["-5", "+5", "  12", "12  ", "0007", "-0", "-", "", "1,000", "1.000", "999999999", "2147483647", "2147483648", "-2147483648", "-2147483649",
                "999999999999999999", "9223372036854775807", "9223372036854775808", "-9223372036854775808", "−5", "1e3", "12x"];
            var random = new Random(42);
            var values = Enumerable.Range(0, 2000).Select(_ => random.NextInt64(long.MinValue, long.MaxValue) >> random.Next(0, 63)).Select(v => v.ToString(CultureInfo.InvariantCulture));

            foreach (string text in edges.Concat(values))
            {
                byte[] bytes = Bytes(text);
                bool expected = long.TryParse(bytes, culture, out long bclLong);
                bool actual = Infrastructure.FixedWidthRuntime.TryParse(bytes, in context, in options, out long parsedLong);
                Assert.True(expected == actual && (!expected || bclLong == parsedLong), $"long '{text}': BCL {expected} {bclLong}, parser {actual} {parsedLong}");
                expected = int.TryParse(bytes, culture, out int bclInt);
                actual = Infrastructure.FixedWidthRuntime.TryParse(bytes, in context, in options, out int parsedInt);
                Assert.True(expected == actual && (!expected || bclInt == parsedInt), $"int '{text}': BCL {expected} {bclInt}, parser {actual} {parsedInt}");
            }
        }

        // The integer and date fast paths for writing must lay out the same bytes as the BCL's char path.
        [Theory]
        [InlineData("")]
        [InlineData("de-DE")]
        [InlineData("th-TH")]
        [InlineData("sv-SE")]
        public void Format_IntegersAndDates_MatchTheBcl(string cultureName)
        {
            var context = CtxWith(CultureInfo.GetCultureInfo(cultureName));
            var random = new Random(42);
            long[] edges = [0, 1, -1, 9, -9, 10, 123, -171, int.MaxValue, int.MinValue, long.MaxValue, long.MinValue];
            var longs = edges.Concat(Enumerable.Range(0, 1000).Select(_ => random.NextInt64(long.MinValue, long.MaxValue) >> random.Next(0, 63)));
            var dates = Enumerable.Range(0, 300).Select(_ => new DateTime(1, 1, 1).AddSeconds(random.NextInt64(0, (long)(DateTime.MaxValue - DateTime.MinValue).TotalSeconds)));
            string[] formats = ["yyyyMMdd", "yyyy-MM-dd", "dd/MM/yyyy", "yyyy-MM-ddTHH:mm:ss", "HHmmss", "HH:mm:ss"];

            foreach (var alignment in new[] { Alignment.Left, Alignment.Right })
            {
                foreach (char padding in new[] { ' ', '0' })
                {
                    var plain = new Infrastructure.ColumnFormatOptions(alignment, padding, null, OverflowBehavior.Throw);
                    foreach (long value in longs)
                    {
                        // Room for sv-SE's three-byte minus sign too.
                        Same(value, plain, 23);
                        Same((int)value, plain, 14);
                    }
                    foreach (string format in formats)
                    {
                        var options = new Infrastructure.ColumnFormatOptions(alignment, padding, format, OverflowBehavior.Throw);
                        foreach (var date in dates)
                        {
                            Same(date, options, 24);
                            if (!format.Contains('H', StringComparison.Ordinal))
                            {
                                Same(DateOnly.FromDateTime(date), options, 24);
                            }
                            if (!format.Contains('y', StringComparison.Ordinal))
                            {
                                Same(TimeOnly.FromDateTime(date), options, 24);
                            }
                        }
                    }
                }
            }

            void Same<T>(T value, Infrastructure.ColumnFormatOptions options, int width)
                where T : IUtf8SpanFormattable, ISpanFormattable
            {
                byte[] fast = new byte[width];
                byte[] bcl = new byte[width];
                Infrastructure.FixedWidthRuntime.FormatUtf8Value(value, fast, in context, options, "c");
                Infrastructure.FixedWidthRuntime.FormatValue(value, bcl, in context, options, "c");
                Assert.True(fast.AsSpan().SequenceEqual(bcl), $"{typeof(T).Name} {value} ({options.Format}, {options.Alignment}, '{options.Padding}'): fast \"{Encoding.UTF8.GetString(fast)}\", BCL \"{Encoding.UTF8.GetString(bcl)}\"");
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
