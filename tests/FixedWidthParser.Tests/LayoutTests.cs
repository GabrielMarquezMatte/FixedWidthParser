using System.Text;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>A layout built from attributes parses and formats through the <see cref="FixedWidth"/> overloads.</summary>
    public class LayoutTests
    {
        [Fact]
        public void FromAttributes_TryParse_MatchesParser()
        {
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();

            Assert.True(FixedWidth.TryParse("John Doe  30   6000000.00"u8, layout, Ctx, out var p));
            Assert.Equal(("John Doe", 30, 6000000.00), (p.Name, p.Age, p.Salary));
        }

        [Fact]
        public void FromAttributes_TryFormat_WritesLine()
        {
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();
            Span<byte> buffer = stackalloc byte[25];

            Assert.True(FixedWidth.TryFormat(new PersonModel { Name = "Ann", Age = 7, Salary = 1.5 }, layout, buffer, Ctx, out int written));
            Assert.Equal("Ann       7    1.5       ", Encoding.UTF8.GetString(buffer[..written]));
            Assert.False(FixedWidth.TryFormat(new PersonModel(), layout, stackalloc byte[24], Ctx, out _));
        }

        [Fact]
        public void FromAttributes_IsCachedPerType()
        {
            Assert.Same(FixedWidthLayout.FromAttributes<PersonModel>(), FixedWidthLayout.FromAttributes<PersonModel>());
        }

        [Fact]
        public void FromAttributes_InvalidLayout_ThrowsEveryCall()
        {
            for (int i = 0; i < 2; i++)
            {
                var ex = Assert.Throws<InvalidOperationException>(() => FixedWidthLayout.FromAttributes<OverlapReadModel>());
                Assert.Contains("Overlapping", ex.Message, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void FromAttributes_RefStruct_ParsesButCannotFormat()
        {
            var layout = FixedWidthLayout.FromAttributes<RefPersonModel>();

            Assert.True(FixedWidth.TryParse("John Doe  30   6000000.00"u8, layout, Ctx, out var r));
            Assert.Equal(30, r.Age);
            Assert.Throws<InvalidOperationException>(() => FixedWidth.TryFormat(new RefPersonModel(), layout, new byte[25], Ctx, out _));
        }
    }
}
