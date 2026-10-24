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

        // More values than the per-thread cache in front of the pool holds, so sets fill, evict and collide:
        // every value must still decode right. (The pool evicts too, so instances are not compared here.)
        [Fact]
        public void TryParse_WithStringPool_ManyValues_DecodeRight()
        {
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();
            var pool = new StringPool();
            var context = CtxWith(pool: pool);
            var names = Enumerable.Range(0, 5000).Select(i => i % 3 == 0 ? "é" + i : "N" + i).ToArray();

            for (int round = 0; round < 2; round++)
            {
                foreach (string name in names)
                {
                    byte[] line = [.. Bytes(name), .. Enumerable.Repeat((byte)' ', 10 - Bytes(name).Length), .. "25   100.00    "u8];
                    Assert.True(FixedWidth.TryParse(line, layout, context, out var model));
                    Assert.Equal(name, model.Name);
                }
            }
        }

        [Fact]
        public void TryParse_WithStringPool_OtherEncodingsAndStrictUtf8_DecodeAsWithoutIt()
        {
            var layout = FixedWidthLayout.FromAttributes<CodeModel>();
            var latin1 = CtxWith(pool: new StringPool(), encoding: System.Text.Encoding.Latin1);
            var strict = CtxWith(pool: new StringPool(), encoding: new System.Text.UTF8Encoding(false, true));

            Assert.True(FixedWidth.TryParse([0xC9, 0x41, 0x42], layout, latin1, out var model));
            Assert.Equal("ÉAB", model.Code);
            Assert.True(FixedWidth.TryParse("éA"u8, layout, strict, out model));
            Assert.Equal("éA", model.Code);
            Assert.Throws<System.Text.DecoderFallbackException>(() => FixedWidth.TryParse([0xC3, 0x41, 0x42], layout, strict, out _));
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
