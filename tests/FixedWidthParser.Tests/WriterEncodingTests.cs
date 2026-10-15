using System.Text;
using FixedWidthParser.Writers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>The writer's encoding, byte-based truncation and line break.</summary>
    public class WriterEncodingTests
    {
        [Fact]
        public void Latin1_WritesOneBytePerCharacter()
        {
            var writer = new FixedWidthWriter<NameAgeModel>(new FixedWidthWriterOptions { Encoding = Encoding.Latin1, NewLine = "\n" });
            using var ms = new MemoryStream();

            writer.Write(ms, new NameAgeModel { Name = "José", Age = 7 });

            Assert.Equal(Encoding.Latin1.GetBytes("José  7  \n"), ms.ToArray());
        }

        [Fact]
        public void Utf8_MultiByteCharacters_CountAsBytes()
        {
            // "José" is 5 bytes in UTF-8, so one pad byte is left in the 6-byte column.
            string line = WriteOne(new FixedWidthWriter<NameAgeModel>(), new NameAgeModel { Name = "José", Age = 7 });

            Assert.Equal("José 7  ", line);
        }

        [Fact]
        public void Utf8_Truncation_NeverSplitsACharacter()
        {
            // "ABCDEé" needs 7 bytes; the 6-byte column keeps "ABCDE" and pads, instead of half of 'é'.
            string line = WriteOne(new FixedWidthWriter<NameAgeModel>(), new NameAgeModel { Name = "ABCDEé", Age = 1 });

            Assert.Equal("ABCDE 1  ", line);
        }

        [Fact]
        public void Utf8_RightAlignedTruncation_KeepsWholeTrailingCharacters()
        {
            // Right-aligned width 4 keeps the rightmost whole characters: "éF" is 3 bytes, "DéF" would be 4.
            string line = WriteOne(new FixedWidthWriter<RightTruncateModel>(), new RightTruncateModel { Code = "ABCDéF" });

            Assert.Equal("DéF", line);
        }

        [Fact]
        public void Generated_MatchesReflection_ForLatin1()
        {
            var context = CtxWith(encoding: Encoding.Latin1);
            var writer = new FixedWidthWriter<NameAgeModel>(new FixedWidthWriterOptions { Encoding = Encoding.Latin1 });
            Span<byte> generated = stackalloc byte[9];
            Span<byte> reflected = stackalloc byte[9];

            Assert.True(FixedWidth.TryFormat(new GenNameAgeModel { Name = "Joséé", Age = 42 }, generated, context, out int g));
            Assert.True(writer.TryFormat(new NameAgeModel { Name = "Joséé", Age = 42 }, reflected, out int r));

            Assert.Equal(9, g);
            Assert.Equal(9, r);
            Assert.True(generated.SequenceEqual(reflected));
            Assert.Equal("Joséé 42 ", Encoding.Latin1.GetString(generated));
        }

        [Fact]
        public void NewLine_IsConfigurable()
        {
            var writer = new FixedWidthWriter<CodeModel>(new FixedWidthWriterOptions { NewLine = "\r\n" });

            Assert.Equal("ABC\r\nDEF\r\n", WriteMany(writer, new[] { new CodeModel { Code = "ABC" }, new CodeModel { Code = "DEF" } }));
        }

        [Fact]
        public void TryFormat_DestinationTooSmall_ReturnsFalse()
        {
            var writer = new FixedWidthWriter<CodeModel>();

            Assert.False(writer.TryFormat(new CodeModel { Code = "ABC" }, new byte[2], out int written));
            Assert.Equal(0, written);
        }

        [Fact]
        public void WriteMany_ManyRecords_SpansSeveralBatches()
        {
            var models = Enumerable.Range(0, 20_000).Select(i => new CodeModel { Code = (i % 1000).ToString("D3", Inv) }).ToArray();
            var writer = new FixedWidthWriter<CodeModel>(new FixedWidthWriterOptions { NewLine = "\n" });

            string fromSpan = WriteMany(writer, models.AsSpan());
            string fromEnumerable = WriteMany(writer, (IEnumerable<CodeModel>)models);

            Assert.Equal(20_000 * 4, fromSpan.Length);
            Assert.Equal(fromSpan, fromEnumerable);
            Assert.EndsWith("999\n", fromSpan, StringComparison.Ordinal);
        }
    }
}
