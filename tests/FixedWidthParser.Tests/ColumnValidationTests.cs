using static FixedWidthParser.Tests.TestHelpers;

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
            var ex = Assert.Throws<InvalidOperationException>(() => FixedWidth.TryParse(ReadOnlySpan<byte>.Empty, FixedWidthLayout.FromAttributes<OverlapReadModel>(), Ctx, out _));
            Assert.Contains("Overlapping", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Writer_OverlappingColumns_ThrowsOnConstruction()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => FixedWidth.WriteMany(Stream.Null, Array.Empty<OverlapWriteModel>(), FixedWidthLayout.FromAttributes<OverlapWriteModel>()));
            Assert.Contains("Overlapping", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Parser_NegativeStart_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => FixedWidth.TryParse(ReadOnlySpan<byte>.Empty, FixedWidthLayout.FromAttributes<NegativeStartModel>(), Ctx, out _));
            Assert.Contains("Start", ex.Message, StringComparison.Ordinal);
        }

        public sealed class FarEndModel
        {
            [FixedColumn(2147483646, 3)] public int Value { get; set; }
        }

        public sealed class FarEdgeModel
        {
            [FixedColumn(2147483646, 1)] public int Value { get; set; }
        }

        [Fact]
        public void ColumnEndingPastIntMaxValue_Throws()
        {
            var attributes = Assert.Throws<InvalidOperationException>(() => FixedWidth.TryParse(ReadOnlySpan<byte>.Empty, FixedWidthLayout.FromAttributes<FarEndModel>(), Ctx, out _));
            var fluent = Assert.Throws<InvalidOperationException>(() => FixedWidthLayout.Build<FarEndModel>(b => b
                .Column("Value", 2147483646, 3, (in FarEndModel m) => m.Value, (ref FarEndModel m, int v) => m.Value = v)));

            Assert.Contains("ends past int.MaxValue", attributes.Message, StringComparison.Ordinal);
            Assert.Contains("ends past int.MaxValue", fluent.Message, StringComparison.Ordinal);
            Assert.False(FixedWidth.TryParse(ReadOnlySpan<byte>.Empty, FixedWidthLayout.FromAttributes<FarEdgeModel>(), Ctx, out _));
        }

        [Fact]
        public void Writer_ZeroLength_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => FixedWidth.WriteMany(Stream.Null, Array.Empty<ZeroLengthModel>(), FixedWidthLayout.FromAttributes<ZeroLengthModel>()));
            Assert.Contains("Length", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Parser_NoColumns_ThrowsOnConstruction()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => FixedWidth.TryParse(ReadOnlySpan<byte>.Empty, FixedWidthLayout.FromAttributes<NoColumnsModel>(), Ctx, out _));
            Assert.Contains("no [FixedColumn]", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Parser_NonAsciiTrimChar_ThrowsOnConstruction()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => FixedWidth.TryParse(ReadOnlySpan<byte>.Empty, FixedWidthLayout.FromAttributes<NonAsciiTrimModel>(), Ctx, out _));
            Assert.Contains("TrimChar", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Writer_NonAsciiPadding_ThrowsOnConstruction()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => FixedWidth.WriteMany(Stream.Null, Array.Empty<NonAsciiPaddingModel>(), FixedWidthLayout.FromAttributes<NonAsciiPaddingModel>()));
            Assert.Contains("Padding", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Parser_UnsupportedColumnType_ThrowsOnConstruction()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => FixedWidth.TryParse(ReadOnlySpan<byte>.Empty, FixedWidthLayout.FromAttributes<UnsupportedTypeModel>(), Ctx, out _));
            Assert.Contains("Converter", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Writer_NoColumns_ThrowsOnConstruction()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => FixedWidth.WriteMany(Stream.Null, Array.Empty<NoColumnsModel>(), FixedWidthLayout.FromAttributes<NoColumnsModel>()));
            Assert.Contains("no [FixedColumn]", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AdjacentColumns_AreValid()
        {
            // Touching columns (end of one == start of the next) do not overlap.
            var parser = FixedWidthLayout.FromAttributes<AdjacentColumnsModel>();
            var writer = FixedWidthLayout.FromAttributes<AdjacentColumnsModel>();

            Assert.NotNull(parser);
            Assert.NotNull(writer);
        }
    }
}
