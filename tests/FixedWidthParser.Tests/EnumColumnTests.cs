using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    public enum Side { Buy = 1, Sell = 2 }

    [Flags]
    public enum Access { None = 0, Read = 1, Write = 2 }

    public sealed class EnumModel
    {
        [FixedColumn(0, 10)] public Side Side { get; set; }
        [FixedColumn(10, 5)] public Side? MaybeSide { get; set; }
        [FixedColumn(15, 10)] public Access Access { get; set; }
    }

    public partial class GenEnumModel : IFixedWidthModel<GenEnumModel>
    {
        [FixedColumn(0, 10)] public Side Side { get; set; }
        [FixedColumn(10, 5)] public Side? MaybeSide { get; set; }
        [FixedColumn(15, 10)] public Access Access { get; set; }
    }

    public enum Kind { Ação = 1, Fundo = 2 }

    public sealed class KindModel
    {
        [FixedColumn(0, 6)] public Kind Kind { get; set; }
    }

    public sealed class EnumCodeModel
    {
        [FixedColumn(0, 2, Format = "D", Padding = '0', Alignment = Alignment.Right)] public Side Side { get; set; }
    }

    public partial class GenEnumCodeModel : IFixedWidthModel<GenEnumCodeModel>
    {
        [FixedColumn(0, 2, Format = "D", Padding = '0', Alignment = Alignment.Right)] public Side Side { get; set; }
    }

    /// <summary>
    /// Enum columns, as ExcelReader reads them: a member's name in any case or its number, nothing the enum does not
    /// define. Attribute, generated and fluent layouts agree.
    /// </summary>
    public class EnumColumnTests
    {
        private static byte[] Line(string side, string maybeSide = "", string access = "Read")
        {
            return Bytes(side.PadRight(10) + maybeSide.PadRight(5) + access.PadRight(10));
        }

        private static FixedWidthLayout<EnumModel> Fluent()
        {
            return FixedWidthLayout.Build<EnumModel>(b => b
                .Factory(() => new EnumModel())
                .EnumColumn("Side", 0, 10, (in EnumModel m) => m.Side, (ref EnumModel m, Side v) => m.Side = v)
                .NullableEnumColumn("MaybeSide", 10, 5, (in EnumModel m) => m.MaybeSide, (ref EnumModel m, Side? v) => m.MaybeSide = v)
                .EnumColumn("Access", 15, 10, (in EnumModel m) => m.Access, (ref EnumModel m, Access v) => m.Access = v));
        }

        [Theory]
        [InlineData("Buy", Side.Buy)]
        [InlineData("sell", Side.Sell)]
        [InlineData("SELL", Side.Sell)]
        [InlineData("1", Side.Buy)]
        [InlineData("02", Side.Sell)]
        [InlineData("  Sell", Side.Sell)]
        public void NameOrNumber_Parses(string text, Side expected)
        {
            Assert.True(FixedWidth.TryParse(Line(text), FixedWidthLayout.FromAttributes<EnumModel>(), Ctx, out var attributes));
            Assert.True(FixedWidth.TryParse<GenEnumModel>(Line(text), Ctx, out var generated));
            Assert.True(FixedWidth.TryParse(Line(text), Fluent(), Ctx, out var fluent));
            Assert.Equal(expected, attributes.Side);
            Assert.Equal(expected, generated.Side);
            Assert.Equal(expected, fluent.Side);
        }

        [Theory]
        [InlineData("Hold")]
        [InlineData("3")]
        [InlineData("0")]
        [InlineData("")]
        [InlineData("Buy,Sell")]
        [InlineData("0Buy")]
        [InlineData("00")]
        [InlineData("-1")]
        public void UndefinedValue_DoesNotParse(string text)
        {
            Assert.False(FixedWidth.TryParse(Line(text), FixedWidthLayout.FromAttributes<EnumModel>(), Ctx, out _));
            Assert.False(FixedWidth.TryParse<GenEnumModel>(Line(text), Ctx, out _));
            Assert.False(FixedWidth.TryParse(Line(text), Fluent(), Ctx, out _));
        }

        [Fact]
        public void FlagsCombination_DoesNotParse()
        {
            Assert.True(FixedWidth.TryParse(Line("Buy", access: "Write"), FixedWidthLayout.FromAttributes<EnumModel>(), Ctx, out var single));
            Assert.Equal(Access.Write, single.Access);
            Assert.True(FixedWidth.TryParse<GenEnumModel>(Line("Buy", access: "00"), Ctx, out var zero));
            Assert.Equal(Access.None, zero.Access);
            Assert.False(FixedWidth.TryParse(Line("Buy", access: "Read,Write"), FixedWidthLayout.FromAttributes<EnumModel>(), Ctx, out _));
            Assert.False(FixedWidth.TryParse<GenEnumModel>(Line("Buy", access: "3"), Ctx, out _));
        }

        [Fact]
        public void Nullable_BlankIsNull()
        {
            Assert.True(FixedWidth.TryParse(Line("Buy"), FixedWidthLayout.FromAttributes<EnumModel>(), Ctx, out var blank));
            Assert.Null(blank.MaybeSide);
            Assert.True(FixedWidth.TryParse<GenEnumModel>(Line("Buy", "sell"), Ctx, out var present));
            Assert.Equal(Side.Sell, present.MaybeSide);
            Assert.True(FixedWidth.TryParse(Line("Buy", "2"), Fluent(), Ctx, out var fluent));
            Assert.Equal(Side.Sell, fluent.MaybeSide);
        }

        [Fact]
        public void Write_WritesTheName()
        {
            const string expected = "Sell           None      ";
            var model = new EnumModel { Side = Side.Sell, MaybeSide = null, Access = Access.None };

            Assert.Equal(expected, WriteOne(FixedWidthLayout.FromAttributes<EnumModel>(), model));
            Assert.Equal(expected, WriteOne(Fluent(), model));
            Assert.Equal(expected, Format(new GenEnumModel { Side = Side.Sell, MaybeSide = null, Access = Access.None }));
            Assert.Equal("Buy       Sell Read      ", Format(new GenEnumModel { Side = Side.Buy, MaybeSide = Side.Sell, Access = Access.Read }));
        }

        [Fact]
        public void NonAsciiName_IsDecodedWithTheEncoding()
        {
            var latin1 = CtxWith(encoding: System.Text.Encoding.Latin1);

            Assert.True(FixedWidth.TryParse(System.Text.Encoding.Latin1.GetBytes("AÇÃO  "), FixedWidthLayout.FromAttributes<KindModel>(), latin1, out var model));
            Assert.Equal(Kind.Ação, model.Kind);
        }

        [Fact]
        public void FormatD_WritesTheNumber_AndReadsItBack()
        {
            Assert.Equal("02", WriteOne(FixedWidthLayout.FromAttributes<EnumCodeModel>(), new EnumCodeModel { Side = Side.Sell }));
            Assert.Equal("01", Format(new GenEnumCodeModel { Side = Side.Buy }));
            Assert.True(FixedWidth.TryParse<GenEnumCodeModel>("02"u8, Ctx, out var parsed));
            Assert.Equal(Side.Sell, parsed.Side);
        }
    }
}
