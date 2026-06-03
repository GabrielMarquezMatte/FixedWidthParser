using FixedWidthParser.Parsers;
using FixedWidthParser.Writers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Layout validation on construction: Start &gt;= 0, Length &gt;= 1 and no overlapping columns.
    /// Fails early, with a clear message, instead of obscure per-line errors at runtime.
    /// </summary>
    public class ColumnValidationTests
    {
        [Fact]
        public void Parser_OverlappingColumns_ThrowsOnConstruction()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new FixedWidthParser<OverlapReadModel>());
            Assert.Contains("Overlapping", ex.Message);
        }

        [Fact]
        public void Writer_OverlappingColumns_ThrowsOnConstruction()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new FixedWidthWriter<OverlapWriteModel>());
            Assert.Contains("Overlapping", ex.Message);
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
            // Touching columns (end of one == start of the next) do not overlap.
            var parser = new FixedWidthParser<AdjacentColumnsModel>();
            var writer = new FixedWidthWriter<AdjacentColumnsModel>();

            Assert.NotNull(parser);
            Assert.NotNull(writer);
        }
    }
}
