using FixedWidthParser.Native;
using FixedWidthParser.Native.Layouts;
using static FixedWidthParser.Tests.Native.NativeTestLayouts;

namespace FixedWidthParser.Tests.Native
{
    public sealed class NativeLayoutTests
    {
        private static void AssertInvalid(string expectedFragment, params Col[] columns)
        {
            Assert.Equal(NativeStatus.InvalidArgument, Create(out var layout, columns));
            Assert.Null(layout);
            Assert.Contains(expectedFragment, LastError(), StringComparison.Ordinal);
        }

        [Fact]
        public void Create_ValidCotahistColumns_Succeeds()
        {
            var layout = Build(
                new Col("tipreg", 0, 2, TString),
                new Col("data_pregao", 2, 8, TDate) { Format = "yyyyMMdd" },
                new Col("codneg", 12, 12, TString),
                new Col("preabe", 56, 13, TFloat64) { Scale = 2 },
                new Col("quatot", 152, 18, TInt64),
                new Col("voltot", 170, 18, TDecimal) { Scale = 2, Nullable = true });

            Assert.Equal(6, layout.Columns.Length);
            Assert.Equal("preabe", layout.Columns[3].Name);
            Assert.Equal(2, layout.Columns[3].Scale);
            Assert.Equal("yyyyMMdd", layout.Columns[1].Parse.Format);
            Assert.True(layout.Columns[5].Nullable);
        }

        [Fact]
        public void Create_Defaults_FollowTheSpec()
        {
            var layout = Build(new Col("name", 0, 5, TString), new Col("qty", 5, 4, TInt64));

            Assert.Equal(TrimMode.Both, layout.Columns[0].Parse.TrimMode);
            Assert.Equal(' ', layout.Columns[0].Parse.TrimChar);
            Assert.Equal(Alignment.Left, layout.Columns[0].FormatOptions.Alignment);
            Assert.Equal(Alignment.Right, layout.Columns[1].FormatOptions.Alignment);
            Assert.Equal(' ', layout.Columns[1].FormatOptions.Padding);
            Assert.Equal(OverflowBehavior.Throw, layout.Columns[0].FormatOptions.Overflow);
            Assert.Equal((byte)' ', layout.Filler);
        }

        [Fact]
        public void Create_Overlap_NamesBothColumns()
        {
            AssertInvalid("\"a\" [0, 5) and \"b\" [3, 6)", new Col("a", 0, 5, TString), new Col("b", 3, 3, TString));
        }

        [Fact]
        public void Create_NegativeStart_Fails()
        {
            AssertInvalid("negative Start (-1)", new Col("a", -1, 5, TString));
        }

        [Fact]
        public void Create_ZeroLength_Fails()
        {
            AssertInvalid("invalid Length (0)", new Col("a", 0, 0, TString));
        }

        [Fact]
        public void Create_NonAsciiPadding_Fails()
        {
            AssertInvalid("padding", new Col("a", 0, 5, TString) { Padding = 'é' });
        }

        [Fact]
        public void Create_ScaleOutOfRange_Fails()
        {
            AssertInvalid("scale", new Col("a", 0, 20, TFloat64) { Scale = 19 });
        }

        [Fact]
        public void Create_ScaleOnInt64_Fails()
        {
            AssertInvalid("scale", new Col("a", 0, 5, TInt64) { Scale = 2 });
        }

        [Fact]
        public void Create_FormatOnString_Fails()
        {
            AssertInvalid("format", new Col("a", 0, 5, TString) { Format = "x" });
        }

        [Fact]
        public void Create_TruncateOnNumber_Fails()
        {
            AssertInvalid("truncate", new Col("a", 0, 5, TInt64) { Overflow = 1 });
        }

        [Fact]
        public void Create_UnknownType_Fails()
        {
            AssertInvalid("type", new Col("a", 0, 5, 42));
        }

        [Fact]
        public void Create_NoName_Fails()
        {
            AssertInvalid("name", new Col("", 0, 5, TString));
        }

        [Fact]
        public void Create_NoColumns_Fails()
        {
            AssertInvalid("at least one column");
        }

        [Fact]
        public void Create_MixedStructSizes_Fails()
        {
            AssertInvalid("struct_size", new Col("a", 0, 5, TString), new Col("b", 5, 5, TString) { StructSize = 8 });
        }

        [Fact]
        public void Options_UnknownStructSize_IsInvalidArgument()
        {
            var options = Options() with { StructSize = 4 };

            Assert.Equal(NativeStatus.InvalidArgument, Create(out _, options, new Col("a", 0, 5, TString)));
            Assert.Contains("fw_layout_options struct_size is 4", LastError(), StringComparison.Ordinal);
        }

        [Fact]
        public void LineLength_DefaultsToLastColumnEnd()
        {
            var layout = Build(new Col("a", 10, 5, TString), new Col("b", 0, 3, TString));

            Assert.Equal(15, layout.LineLength);
        }

        [Fact]
        public void LineLength_ShorterThanColumns_Fails()
        {
            Assert.Equal(NativeStatus.InvalidArgument, Create(out _, Options(lineLength: 4), new Col("a", 0, 5, TString)));
            Assert.Contains("line_length", LastError(), StringComparison.Ordinal);
        }

        [Fact]
        public void LineLength_AndFiller_AreKept()
        {
            Assert.Equal(NativeStatus.Ok, Create(out var layout, Options(lineLength: 245, filler: '0'), new Col("a", 0, 5, TString)));

            Assert.Equal(245, layout!.LineLength);
            Assert.Equal((byte)'0', layout.Filler);
        }
    }
}
