using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// The source-generated <c>TryParse</c> (via <see cref="FixedWidth.TryParse{TModel}"/>) produces the
    /// same results as the type's <see cref="FixedWidthLayout.FromAttributes{TModel}"/> layout, including what each reports
    /// about a rejected line.
    /// </summary>
    public class SourceGenParityTests
    {
        [Theory]
        [InlineData("John Doe  30   6000000.00")]
        [InlineData("Jane      28   55000.00  ")]
        [InlineData("John")]
        [InlineData("")]
        [InlineData("Ann       abc  10.5      ")]
        public void Person_GeneratedMatchesReflection(string line)
        {
            bool okR = FixedWidth.TryParse(Bytes(line), FixedWidthLayout.FromAttributes<PersonModel>(), Ctx, out var r);
            bool okG = FixedWidth.TryParse<GenPersonModel>(Bytes(line), Ctx, out var g);

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
            Assert.True(FixedWidth.TryParse<GenPersonModel>("John Doe  30   6000000.00"u8, Ctx, out var g));
            Assert.Equal("John Doe", g.Name);
            Assert.Equal(30, g.Age);
            Assert.Equal(6000000.00, g.Salary);
        }

        [Theory]
        [InlineData("ABCDE0042")]
        [InlineData("X    7   ")]
        [InlineData("AB")]
        public void Product_GeneratedMatchesReflection(string line)
        {
            bool okR = FixedWidth.TryParse(Bytes(line), FixedWidthLayout.FromAttributes<ProductModel>(), Ctx, out var r);
            bool okG = FixedWidth.TryParse<GenProductModel>(Bytes(line), Ctx, out var g);

            Assert.Equal(okR, okG);
            Assert.Equal(r.Code, g.Code);
            Assert.Equal(r.Quantity, g.Quantity);
        }

        [Theory]
        [InlineData("3.14159 ")]
        [InlineData("  -2.5  ")]
        [InlineData("notanum ")]
        public void Measurement_GeneratedMatchesReflection(string line)
        {
            bool okR = FixedWidth.TryParse(Bytes(line), FixedWidthLayout.FromAttributes<MeasurementModel>(), Ctx, out var r);
            bool okG = FixedWidth.TryParse<GenMeasurementModel>(Bytes(line), Ctx, out var g);

            Assert.Equal(okR, okG);
            Assert.Equal(r.Value, g.Value);
        }

        [Theory]
        [InlineData("42")]
        [InlineData("42   short     ")]
        public void TrailingString_GeneratedMatchesReflection(string line)
        {
            bool okR = FixedWidth.TryParse(Bytes(line), FixedWidthLayout.FromAttributes<TrailingStringModel>(), Ctx, out var r);
            bool okG = FixedWidth.TryParse<GenTrailingStringModel>(Bytes(line), Ctx, out var g);

            Assert.Equal(okR, okG);
            Assert.Equal(r.Id, g.Id);
            Assert.Equal(r.Note, g.Note);
        }

        [Theory]
        [InlineData("123.45      ")]
        [InlineData("-0.01       ")]
        [InlineData("bad         ")]
        public void Decimal_GeneratedMatchesReflection(string line)
        {
            bool okR = FixedWidth.TryParse(Bytes(line), FixedWidthLayout.FromAttributes<DecimalModel>(), Ctx, out var r);
            bool okG = FixedWidth.TryParse<GenDecimalModel>(Bytes(line), Ctx, out var g);

            Assert.Equal(okR, okG);
            Assert.Equal(r.Amount, g.Amount);
        }

        [Fact]
        public void DecodedParsable_GeneratedMatchesReflection()
        {
            bool okR = FixedWidth.TryParse("hey   "u8, FixedWidthLayout.FromAttributes<ShoutModel>(), Ctx, out var r);
            bool okG = FixedWidth.TryParse<GenShoutModel>("hey   "u8, Ctx, out var g);

            Assert.True(okR);
            Assert.True(okG);
            Assert.Equal("HEY", r.Word.Text);
            Assert.Equal(r.Word, g.Word);
        }

        [Fact]
        public void Latin1_GeneratedMatchesReflection()
        {
            // "Antônio" doesn't fit; "Joséé" is 5 characters and 5 bytes in Latin-1, 7 bytes in UTF-8.
            var context = CtxWith(encoding: Encoding.Latin1);
            byte[] line = Encoding.Latin1.GetBytes("Joséé 42 ");

            Assert.True(FixedWidth.TryParse(line, FixedWidthLayout.FromAttributes<NameAgeModel>(), context, out var r));
            Assert.True(FixedWidth.TryParse<GenNameAgeModel>(line, context, out var g));

            Assert.Equal("Joséé", r.Name);
            Assert.Equal(42, r.Age);
            Assert.Equal(r.Name, g.Name);
            Assert.Equal(r.Age, g.Age);
        }

        [Fact]
        public void RefStruct_GeneratedMatchesReflection()
        {
            Assert.True(FixedWidth.TryParse("John Doe  30   6000000.00"u8, FixedWidthLayout.FromAttributes<RefPersonModel>(), Ctx, out var r));
            Assert.True(FixedWidth.TryParse<GenRefPersonModel>("John Doe  30   6000000.00"u8, Ctx, out var g));

            Assert.Equal(r.Name, g.Name);
            Assert.Equal(r.Age, g.Age);
            Assert.Equal(r.Salary, g.Salary);
        }

        [Fact]
        public void StringPool_InternsAndMatchesReflection()
        {
            var pool = new StringPool();
            var context = CtxWith(pool: pool);

            Assert.True(FixedWidth.TryParse("John Doe  30   6000000.00"u8, FixedWidthLayout.FromAttributes<PersonModel>(), context, out var r));
            Assert.True(FixedWidth.TryParse<GenPersonModel>("John Doe  30   6000000.00"u8, context, out var g));

            Assert.Same(r.Name, g.Name);
            Assert.Same(pool.GetOrAdd("John Doe".AsSpan()), g.Name);
        }
    }
}
