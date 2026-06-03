using FixedWidthParser.Parsers;
using FixedWidthParser.Writers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Validação de layout na construção: Start &gt;= 0, Length &gt;= 1 e sem colunas sobrepostas.
    /// Falha cedo, com mensagem clara, em vez de erros obscuros por linha em tempo de execução.
    /// </summary>
    public class ColumnValidationTests
    {
        [Fact]
        public void Parser_OverlappingColumns_ThrowsOnConstruction()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new FixedWidthParser<OverlapReadModel>());
            Assert.Contains("sobrepostas", ex.Message);
        }

        [Fact]
        public void Writer_OverlappingColumns_ThrowsOnConstruction()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new FixedWidthWriter<OverlapWriteModel>());
            Assert.Contains("sobrepostas", ex.Message);
        }

        [Fact]
        public void Parser_NegativeStart_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new FixedWidthParser<NegativeStartModel>());
            Assert.Contains("Start", ex.Message);
        }

        [Fact]
        public void Writer_ZeroLength_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new FixedWidthWriter<ZeroLengthModel>());
            Assert.Contains("Length", ex.Message);
        }

        [Fact]
        public void AdjacentColumns_AreValid()
        {
            // Colunas encostadas (fim de uma == início da outra) não se sobrepõem.
            var parser = new FixedWidthParser<AdjacentColumnsModel>();
            var writer = new FixedWidthWriter<AdjacentColumnsModel>();

            Assert.NotNull(parser);
            Assert.NotNull(writer);
        }
    }
}
