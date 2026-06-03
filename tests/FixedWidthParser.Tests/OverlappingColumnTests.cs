using FixedWidthParser.Parsers;
using FixedWidthParser.Writers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Documenta o comportamento atual com colunas sobrepostas: a biblioteca NÃO valida
    /// sobreposição. Na leitura cada coluna lê sua fatia de forma independente; na escrita
    /// os formatters são aplicados em ordem e o último sobrescreve a região em comum.
    /// </summary>
    public class OverlappingColumnTests
    {
        [Fact]
        public void Parse_OverlappingColumns_ReadIndependently()
        {
            var parser = new FixedWidthParser<OverlapReadModel>();

            // "HELLOWORLD": Left = [0,5) = "HELLO"; Right = [2,5) = "LLOWO".
            bool ok = parser.TryParse("HELLOWORLD", Inv, null, out var model);

            Assert.True(ok);
            Assert.Equal("HELLO", model.Left);
            Assert.Equal("LLOWO", model.Right);
        }

        [Fact]
        public void Write_OverlappingColumns_LastColumnWinsOverlap()
        {
            var writer = new FixedWidthWriter<OverlapWriteModel>();

            // Left ocupa [0,6); Right ocupa [3,9) e sobrescreve as posições 3..5.
            string line = WriteOne(writer, new OverlapWriteModel { Left = "AAAAAA", Right = "BBBBBB" });

            Assert.Equal("AAABBBBBB", line);
        }
    }
}
