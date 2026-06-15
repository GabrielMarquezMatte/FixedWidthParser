using System.Text;
using CsCheck;
using FixedWidthParser.Attributes;
using FixedWidthParser.Parsers;
using FixedWidthParser.Writers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Property-based round-trip tests (CsCheck). Where example-based <see cref="RoundTripTests"/> pin a
    /// handful of hand-picked rows, these assert the codec contract over a large random sample drawn from
    /// the domain in which round-tripping is lossless: string columns hold non-space text no longer than
    /// the column, integers fit their column width, and decimals fit with at most two fractional digits.
    /// </summary>
    public class PropertyRoundTripTests
    {
        // Letters only: no spaces (trailing spaces are trimmed on read) and length <= column width,
        // so the value survives the write-pad / read-trim round-trip exactly. Empty is included.
        private static readonly Gen<string> GenText =
            Gen.Char['A', 'Z'].Array[0, 12].Select(cs => new string(cs));

        // Decimal strings (incl. sign) <= 12 chars: cents in [-99_999_999, 99_999_999] / 100.
        private static readonly Gen<decimal> GenMoney =
            Gen.Long[-99_999_999, 99_999_999].Select(cents => cents / 100m);

        private static readonly Gen<TextNumberModel> GenTextNumber =
            GenText.Select(Gen.Int[-9_999_999, 99_999_999],
                (text, number) => new TextNumberModel { Text = text, Number = number });

        [Fact]
        public void RoundTrip_TextAndNumber_IsLossless()
        {
            var writer = new FixedWidthWriter<TextNumberModel>();
            var parser = new FixedWidthParser<TextNumberModel>();

            GenTextNumber.Sample(original =>
            {
                string line = WriteOne(writer, original);
                bool ok = parser.TryParse(line, Inv, null, out var parsed);

                Assert.True(ok);
                Assert.Equal(original.Text, parsed.Text);
                Assert.Equal(original.Number, parsed.Number);
            });
        }

        [Fact]
        public void RoundTrip_Decimal_IsExact()
        {
            var writer = new FixedWidthWriter<MoneyModel>();
            var parser = new FixedWidthParser<MoneyModel>();

            GenMoney.Sample(amount =>
            {
                var original = new MoneyModel { Amount = amount };
                string line = WriteOne(writer, original);
                bool ok = parser.TryParse(line, Inv, null, out var parsed);

                Assert.True(ok);
                Assert.Equal(original.Amount, parsed.Amount);
            });
        }

        [Fact]
        public void RoundTrip_Double_F2_PreservesToTwoDecimals()
        {
            var writer = new FixedWidthWriter<DoubleF2Model>();
            var parser = new FixedWidthParser<DoubleF2Model>();

            // Values with at most two fractional digits, formatted "F2": survives to two decimals.
            Gen.Long[-9_999_999, 9_999_999].Select(cents => cents / 100.0).Sample(value =>
            {
                var original = new DoubleF2Model { Value = value };
                string line = WriteOne(writer, original);
                bool ok = parser.TryParse(line, Inv, null, out var parsed);

                Assert.True(ok);
                Assert.Equal(original.Value, parsed.Value, 2);
            });
        }

        [Fact]
        public void CharAndByteParsers_AgreeOnSameLine()
        {
            // The char writer emits ASCII (letters + digits), so byte offsets equal char offsets and the
            // UTF-8 byte parser must produce exactly what the char parser does for the same line.
            var writer = new FixedWidthWriter<TextNumberModel>();
            var charParser = new FixedWidthParser<TextNumberModel>();
            var byteParser = new Utf8FixedWidthParser<TextNumberModel>();

            GenTextNumber.Sample(original =>
            {
                string line = WriteOne(writer, original);

                bool charOk = charParser.TryParse(line, Inv, null, out var fromChar);
                bool byteOk = byteParser.TryParse(Encoding.UTF8.GetBytes(line), Inv, null, out var fromByte);

                Assert.True(charOk);
                Assert.True(byteOk);
                Assert.Equal(fromChar.Text, fromByte.Text);
                Assert.Equal(fromChar.Number, fromByte.Number);
            });
        }
    }

    // ----- Models whose layout makes the generated domain round-trip losslessly -----
    // Top-level + public so the library's compiled-expression accessors can reach them.

    public readonly record struct TextNumberModel
    {
        [FixedColumn(0, 12)] public string Text { get; init; }
        [FixedColumn(12, 8)] public int Number { get; init; }
    }

    public readonly record struct MoneyModel
    {
        [FixedColumn(0, 12)] public decimal Amount { get; init; }
    }

    public readonly record struct DoubleF2Model
    {
        [FixedColumn(0, 12, Format = "F2")] public double Value { get; init; }
    }
}
