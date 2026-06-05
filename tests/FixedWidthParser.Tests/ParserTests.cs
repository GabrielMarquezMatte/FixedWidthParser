using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Parsers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    public class ParserTests
    {
        [Fact]
        public void TryParse_WellFormedLine_ParsesAllColumns()
        {
            var parser = new FixedWidthParser<PersonModel>();

            //                         Name      Age  Salary
            bool ok = parser.TryParse("John Doe  30   60000.00  ", Inv, null, out var model);

            Assert.True(ok);
            Assert.Equal("John Doe", model.Name);
            Assert.Equal(30, model.Age);
            Assert.Equal(60000.00, model.Salary, 2);
        }

        [Fact]
        public void TryParse_TrimsTrailingSpacesOnStringColumns()
        {
            var parser = new FixedWidthParser<PersonModel>();

            parser.TryParse("Al        25   100.00    ", Inv, null, out var model);

            Assert.Equal("Al", model.Name);
        }

        [Fact]
        public void TryParse_InvalidNumber_ReturnsFalse()
        {
            var parser = new FixedWidthParser<PersonModel>();

            bool ok = parser.TryParse("John Doe  XX   60000.00  ", Inv, null, out _);

            Assert.False(ok);
        }

        [Fact]
        public void TryParse_ShortLine_NumericColumnMissing_ReturnsFalse()
        {
            var parser = new FixedWidthParser<PersonModel>();

            // The line ends before the Age column (start 10): the numeric processor fails.
            bool ok = parser.TryParse("John", Inv, null, out _);

            Assert.False(ok);
        }

        [Fact]
        public void TryParse_ShortLine_StringColumnMissing_ReturnsFalse()
        {
            var parser = new FixedWidthParser<TrailingStringModel>();

            bool ok = parser.TryParse("42", Inv, null, out var model);

            Assert.False(ok);
            Assert.Equal(default, model);
        }

        [Fact]
        public void TryParse_WithStringPool_ReturnsInternedInstances()
        {
            var parser = new FixedWidthParser<PersonModel>();
            var pool = new StringPool();

            parser.TryParse("Alice     25   100.00    ", Inv, pool, out var first);
            parser.TryParse("Alice     40   200.00    ", Inv, pool, out var second);

            Assert.Equal("Alice", first.Name);
            Assert.Same(first.Name, second.Name);
        }

        [Fact]
        public void TryParse_WithoutPool_ReturnsDistinctInstances()
        {
            var parser = new FixedWidthParser<PersonModel>();

            parser.TryParse("Alice     25   100.00    ", Inv, null, out var first);
            parser.TryParse("Alice     25   100.00    ", Inv, null, out var second);

            Assert.Equal(first.Name, second.Name);
            Assert.NotSame(first.Name, second.Name);
        }

        [Fact]
        public void TryParse_FieldBasedModel_ParsesFields()
        {
            var parser = new FixedWidthParser<ProductModel>();

            bool ok = parser.TryParse("ABC  12  ", Inv, null, out var product);

            Assert.True(ok);
            Assert.Equal("ABC", product.Code);
            Assert.Equal(12, product.Quantity);
        }

        [Fact]
        public void TryParse_FloatColumn_Parses()
        {
            var parser = new FixedWidthParser<MeasurementModel>();

            bool ok = parser.TryParse("3.14    ", Inv, null, out var measurement);

            Assert.True(ok);
            Assert.Equal(3.14f, measurement.Value, 2);
        }
    }
}
