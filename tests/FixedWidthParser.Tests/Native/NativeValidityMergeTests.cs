using System.Runtime.InteropServices;
using FixedWidthParser.Native.Layouts;
using FixedWidthParser.Native.Reading;

namespace FixedWidthParser.Tests.Native
{
    /// <summary>Merging the partitions' validity bitmaps must match a merge done one row at a time, at every bit offset.</summary>
    public sealed unsafe class NativeValidityMergeTests
    {
        private static readonly NativeColumn Column = new()
        {
            Name = "n",
            Length = 1,
            Type = NativeType.Int64,
            Nullable = true,
            Parse = new(' ', TrimMode.Both, null),
        };

        private static bool[] Nulls(int rows, Func<int, bool> isNull)
        {
            return [.. Enumerable.Range(0, rows).Select(isNull)];
        }

        // The lead partition's length puts every later partition's first row at each bit offset in turn.
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        public void Concat_Validity_MatchesTheRowByRowMerge(int lead)
        {
            bool[][] partitions =
            [
                Nulls(lead, r => r % 3 == 0),
                Nulls(37, r => (r * 7 + lead) % 10 == 0),
                Nulls(16, _ => false),
                Nulls(24, _ => true),
                Nulls(0, _ => false),
                Nulls(65, r => r is 0 or 64),
                Nulls(9, r => r == 8),
                Nulls(8, r => r == 7),
            ];
            var parts = partitions.Select(nulls =>
            {
                var part = new ColumnBuilder(Column, int.MaxValue, nulls.Length);
                foreach (bool isNull in nulls)
                {
                    Assert.True(part.TryAppend(isNull ? " "u8 : "1"u8, default));
                }
                return part;
            }).ToArray();
            NativeColumnRaw merged = default;
            try
            {
                ColumnBuilder.Concat(parts, &merged);

                bool[] all = [.. partitions.SelectMany(p => p)];
                byte[] expected = new byte[(all.Length + 7) / 8];
                expected.AsSpan().Fill(0xFF);
                for (int row = 0; row < all.Length; row++)
                {
                    if (all[row])
                    {
                        expected[row >> 3] &= (byte)~(1 << (row & 7));
                    }
                }
                Assert.Equal(all.Length, merged.Length);
                Assert.Equal(expected, new ReadOnlySpan<byte>(merged.Validity, expected.Length).ToArray());
            }
            finally
            {
                Marshal.FreeHGlobal((nint)merged.Values);
                Marshal.FreeHGlobal((nint)merged.Validity);
                foreach (var part in parts)
                {
                    part.Dispose();
                }
            }
        }
    }
}
