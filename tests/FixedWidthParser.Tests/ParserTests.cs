using CommunityToolkit.HighPerformance.Buffers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    public class ParserTests
    {
        [Fact]
        public void TryParse_WellFormedLine_ParsesAllColumns()
        {
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();

            //                                Name      Age  Salary
            bool ok = FixedWidth.TryParse("John Doe  30   60000.00  "u8, layout, Ctx, out var model);

            Assert.True(ok);
            Assert.Equal("John Doe", model.Name);
            Assert.Equal(30, model.Age);
            Assert.Equal(60000.00, model.Salary, 2);
        }

        [Fact]
        public void TryParse_TrimsTrailingSpacesOnStringColumns()
        {
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();

            FixedWidth.TryParse("Al        25   100.00    "u8, layout, Ctx, out var model);

            Assert.Equal("Al", model.Name);
        }

        [Fact]
        public void TryParse_InvalidNumber_ReturnsFalse()
        {
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();

            bool ok = FixedWidth.TryParse("John Doe  XX   60000.00  "u8, layout, Ctx, out _);

            Assert.False(ok);
        }

        [Fact]
        public void TryParse_ShortLine_ReturnsFalse()
        {
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();

            bool ok = FixedWidth.TryParse("John"u8, layout, Ctx, out _);

            Assert.False(ok);
        }

        [Fact]
        public void TryParse_ShortLine_StringColumnMissing_ReturnsFalse()
        {
            var layout = FixedWidthLayout.FromAttributes<TrailingStringModel>();

            bool ok = FixedWidth.TryParse("42"u8, layout, Ctx, out var model);

            Assert.False(ok);
            Assert.Equal(default, model);
        }

        [Fact]
        public void TryParse_WithStringPool_ReturnsInternedInstances()
        {
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();
            var context = CtxWith(pool: new StringPool());

            FixedWidth.TryParse("Alice     25   100.00    "u8, layout, context, out var first);
            FixedWidth.TryParse("Alice     40   200.00    "u8, layout, context, out var second);

            Assert.Equal("Alice", first.Name);
            Assert.Same(first.Name, second.Name);
        }

        [Fact]
        public void TryParse_WithoutPool_ReturnsDistinctInstances()
        {
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();

            FixedWidth.TryParse("Alice     25   100.00    "u8, layout, Ctx, out var first);
            FixedWidth.TryParse("Alice     25   100.00    "u8, layout, Ctx, out var second);

            Assert.Equal(first.Name, second.Name);
            Assert.NotSame(first.Name, second.Name);
        }

        [Fact]
        public void TryParse_FieldBasedModel_ParsesFields()
        {
            var layout = FixedWidthLayout.FromAttributes<ProductModel>();

            bool ok = FixedWidth.TryParse("ABC  12  "u8, layout, Ctx, out var product);

            Assert.True(ok);
            Assert.Equal("ABC", product.Code);
            Assert.Equal(12, product.Quantity);
        }

        [Fact]
        public void TryParse_FloatColumn_Parses()
        {
            var layout = FixedWidthLayout.FromAttributes<MeasurementModel>();

            bool ok = FixedWidth.TryParse("3.14    "u8, layout, Ctx, out var measurement);

            Assert.True(ok);
            Assert.Equal(3.14f, measurement.Value, 2);
        }

        [Fact]
        public void TryParse_StructWithoutDeclaredConstructor_Parses()
        {
            var layout = FixedWidthLayout.FromAttributes<NoParameterlessCtorModel>();

            Assert.True(FixedWidth.TryParse("ABC"u8, layout, Ctx, out var model));
            Assert.Equal("ABC", model.Code);
        }

        [Fact]
        public void Constructor_BuildError_IsRethrownOnEveryConstruction()
        {
            // The error is captured once in the static constructor and rethrown on each construction.
            Assert.Throws<InvalidOperationException>(() => FixedWidth.TryParse(ReadOnlySpan<byte>.Empty, FixedWidthLayout.FromAttributes<OverlapReadModel>(), Ctx, out _));
            Assert.Throws<InvalidOperationException>(() => FixedWidth.TryParse(ReadOnlySpan<byte>.Empty, FixedWidthLayout.FromAttributes<OverlapReadModel>(), Ctx, out _));
        }

        [Fact]
        public void TryParse_RefStructModel_Parses()
        {
            var layout = FixedWidthLayout.FromAttributes<RefPersonModel>();

            bool ok = FixedWidth.TryParse("John Doe  30   60000.00  "u8, layout, Ctx, out var model);

            Assert.True(ok);
            Assert.Equal("John Doe", model.Name);
            Assert.Equal(30, model.Age);
        }
    }
}
