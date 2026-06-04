using System.Globalization;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Parsers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Verifies the source-generated <c>TryParse</c> (via <see cref="FixedWidth.TryParse{TModel}"/>)
    /// produces identical results to the runtime, reflection-based <see cref="FixedWidthParser{TModel}"/>.
    /// </summary>
    public class SourceGenParityTests
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [Theory]
        [InlineData("John Doe  30   6000000.00")] // clean, full width
        [InlineData("Jane      28   55000.00  ")] // trailing spaces in the numeric column
        [InlineData("John")]                       // short line: numeric columns out of bounds
        [InlineData("")]                           // empty line
        [InlineData("Ann       abc  10.5      ")]  // non-numeric age: must be rejected by both
        public void Person_GeneratedMatchesReflection(string line)
        {
            var reflection = new FixedWidthParser<PersonModel>();
            bool okR = reflection.TryParse(line, Inv, null, out var r);
            bool okG = FixedWidth.TryParse<GenPersonModel>(line, Inv, null, out var g);

            Assert.Equal(okR, okG);
            if (okR)
            {
                Assert.Equal(r.Name, g.Name);
                Assert.Equal(r.Age, g.Age);
                Assert.Equal(r.Salary, g.Salary);
            }
        }

        [Fact]
        public void Person_ParsesExpectedValues()
        {
            bool ok = FixedWidth.TryParse<GenPersonModel>("John Doe  30   6000000.00", Inv, null, out var g);
            Assert.True(ok);
            Assert.Equal("John Doe", g.Name);
            Assert.Equal(30, g.Age);
            Assert.Equal(6000000.00, g.Salary);
        }

        [Theory]
        [InlineData("ABCDE0042")]
        [InlineData("X    7   ")]
        [InlineData("AB")] // short: Quantity out of bounds -> rejected by both
        public void Product_GeneratedMatchesReflection(string line)
        {
            var reflection = new FixedWidthParser<ProductModel>();
            bool okR = reflection.TryParse(line, Inv, null, out var r);
            bool okG = FixedWidth.TryParse<GenProductModel>(line, Inv, null, out var g);

            Assert.Equal(okR, okG);
            if (okR)
            {
                Assert.Equal(r.Code, g.Code);
                Assert.Equal(r.Quantity, g.Quantity);
            }
        }

        [Theory]
        [InlineData("3.14159 ")]
        [InlineData("  -2.5  ")]
        [InlineData("notanum ")]
        public void Measurement_GeneratedMatchesReflection(string line)
        {
            var reflection = new FixedWidthParser<MeasurementModel>();
            bool okR = reflection.TryParse(line, Inv, null, out var r);
            bool okG = FixedWidth.TryParse<GenMeasurementModel>(line, Inv, null, out var g);

            Assert.Equal(okR, okG);
            if (okR)
            {
                Assert.Equal(r.Value, g.Value);
            }
        }

        [Theory]
        [InlineData("123.45      ")]
        [InlineData("-0.01       ")]
        [InlineData("bad         ")]
        public void Decimal_GeneratedMatchesReflection(string line)
        {
            var reflection = new FixedWidthParser<DecimalModel>();
            bool okR = reflection.TryParse(line, Inv, null, out var r);
            bool okG = FixedWidth.TryParse<GenDecimalModel>(line, Inv, null, out var g);

            Assert.Equal(okR, okG);
            if (okR)
            {
                Assert.Equal(r.Amount, g.Amount);
            }
        }

        [Fact]
        public void RefStruct_GeneratedMatchesReflection()
        {
            const string line = "John Doe  30   6000000.00";
            var reflection = new FixedWidthParser<RefPersonModel>();
            bool okR = reflection.TryParse(line, Inv, null, out var r);
            bool okG = FixedWidth.TryParse<GenRefPersonModel>(line, Inv, null, out var g);

            Assert.True(okR);
            Assert.Equal(okR, okG);
            Assert.Equal(r.Name, g.Name);
            Assert.Equal(r.Age, g.Age);
            Assert.Equal(r.Salary, g.Salary);
        }

        [Fact]
        public void StringPool_InternsAndMatchesReflection()
        {
            const string line = "John Doe  30   6000000.00";
            var pool = new StringPool();
            var reflection = new FixedWidthParser<PersonModel>();

            bool okR = reflection.TryParse(line, Inv, pool, out var r);
            bool okG = FixedWidth.TryParse<GenPersonModel>(line, Inv, pool, out var g);

            Assert.True(okR);
            Assert.Equal(okR, okG);
            Assert.Equal(r.Name, g.Name);
            // Same pooled instance is returned for the same content.
            Assert.Same(pool.GetOrAdd("John Doe".AsSpan()), g.Name);
        }
    }
}
