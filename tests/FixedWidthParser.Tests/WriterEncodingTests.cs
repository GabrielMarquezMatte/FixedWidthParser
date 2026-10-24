using System.Text;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>The writer's encoding, byte-based truncation and line break.</summary>
    public class WriterEncodingTests
    {
        [Fact]
        public void Latin1_WritesOneBytePerCharacter()
        {
            var writerOptions = new FixedWidthWriterOptions { Encoding = Encoding.Latin1, NewLine = "\n" };
            var layout = FixedWidthLayout.FromAttributes<NameAgeModel>();
            using var ms = new MemoryStream();

            FixedWidth.Write(ms, new NameAgeModel { Name = "José", Age = 7 }, layout, writerOptions);

            Assert.Equal(Encoding.Latin1.GetBytes("José  7  \n"), ms.ToArray());
        }

        [Fact]
        public void Utf8_MultiByteCharacters_CountAsBytes()
        {
            // "José" is 5 bytes in UTF-8, so one pad byte is left in the 6-byte column.
            string line = WriteOne(FixedWidthLayout.FromAttributes<NameAgeModel>(), new NameAgeModel { Name = "José", Age = 7 });

            Assert.Equal("José 7  ", line);
        }

        [Fact]
        public void Utf8_Truncation_NeverSplitsACharacter()
        {
            // "ABCDEé" needs 7 bytes; the 6-byte column keeps "ABCDE" and pads, instead of half of 'é'.
            string line = WriteOne(FixedWidthLayout.FromAttributes<NameAgeModel>(), new NameAgeModel { Name = "ABCDEé", Age = 1 });

            Assert.Equal("ABCDE 1  ", line);
        }

        [Fact]
        public void Utf8_RightAlignedTruncation_KeepsWholeTrailingCharacters()
        {
            // Right-aligned width 4 keeps the rightmost whole characters: "éF" is 3 bytes, "DéF" would be 4.
            string line = WriteOne(FixedWidthLayout.FromAttributes<RightTruncateModel>(), new RightTruncateModel { Code = "ABCDéF" });

            Assert.Equal("DéF", line);
        }

        [Fact]
        public void Generated_MatchesReflection_ForLatin1()
        {
            var context = CtxWith(encoding: Encoding.Latin1);
            var writerOptions = new FixedWidthWriterOptions { Encoding = Encoding.Latin1 };
            var layout = FixedWidthLayout.FromAttributes<NameAgeModel>();
            Span<byte> generated = stackalloc byte[9];
            Span<byte> reflected = stackalloc byte[9];

            Assert.True(FixedWidth.TryFormat(new GenNameAgeModel { Name = "Joséé", Age = 42 }, generated, context, out int g));
            Assert.True(FixedWidth.TryFormat(new NameAgeModel { Name = "Joséé", Age = 42 }, layout, reflected, writerOptions.Context, out int r));

            Assert.Equal(9, g);
            Assert.Equal(9, r);
            Assert.True(generated.SequenceEqual(reflected));
            Assert.Equal("Joséé 42 ", Encoding.Latin1.GetString(generated));
        }

        // Numbers format straight to UTF-8; a non-ASCII result must still be re-encoded for other encodings.
        [Theory]
        [InlineData("utf-8", "  é5.00")]
        [InlineData("iso-8859-1", "   é5.00")]
        public void NonAsciiFormattedValue_UsesTheConfiguredEncoding(string encodingName, string expected)
        {
            var encoding = Encoding.GetEncoding(encodingName);
            var culture = (System.Globalization.CultureInfo)Inv.Clone();
            culture.NumberFormat.CurrencySymbol = "é";
            var writerOptions = new FixedWidthWriterOptions { Encoding = encoding, FormatProvider = culture };
            var layout = FixedWidthLayout.FromAttributes<PriceModel>();
            Span<byte> generated = stackalloc byte[8];
            Span<byte> reflected = stackalloc byte[8];

            Assert.True(FixedWidth.TryFormat(new GenPriceModel { Price = 5m }, generated, CtxWith(culture, encoding: encoding), out _));
            Assert.True(FixedWidth.TryFormat(new PriceModel { Price = 5m }, layout, reflected, writerOptions.Context, out _));

            Assert.Equal(encoding.GetBytes(expected), generated.ToArray());
            Assert.Equal(encoding.GetBytes(expected), reflected.ToArray());
        }

        [Fact]
        public void NewLine_IsConfigurable()
        {
            var writerOptions = new FixedWidthWriterOptions { NewLine = "\r\n" };
            var layout = FixedWidthLayout.FromAttributes<CodeModel>();

            Assert.Equal("ABC\r\nDEF\r\n", WriteMany(layout, new[] { new CodeModel { Code = "ABC" }, new CodeModel { Code = "DEF" } }, writerOptions));
        }

        [Fact]
        public void TryFormat_DestinationTooSmall_ReturnsFalse()
        {
            var layout = FixedWidthLayout.FromAttributes<CodeModel>();

            Assert.False(FixedWidth.TryFormat(new CodeModel { Code = "ABC" }, layout, new byte[2], Ctx, out int written));
            Assert.Equal(0, written);
        }

        [Fact]
        public void WriteMany_ManyRecords_SpansSeveralBatches()
        {
            var models = Enumerable.Range(0, 20_000).Select(i => new CodeModel { Code = (i % 1000).ToString("D3", Inv) }).ToArray();
            var writerOptions = new FixedWidthWriterOptions { NewLine = "\n" };
            var layout = FixedWidthLayout.FromAttributes<CodeModel>();

            string fromSpan = WriteMany(layout, models.AsSpan(), writerOptions);
            string fromEnumerable = WriteMany(layout, (IEnumerable<CodeModel>)models, writerOptions);

            Assert.Equal(20_000 * 4, fromSpan.Length);
            Assert.Equal(fromSpan, fromEnumerable);
            Assert.EndsWith("999\n", fromSpan, StringComparison.Ordinal);
        }
    }
}
