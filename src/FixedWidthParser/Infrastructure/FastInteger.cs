using System.Runtime.CompilerServices;

namespace FixedWidthParser.Infrastructure
{
    /// <summary>
    /// ExcelReader's ASCII-digit fast path for integers: digits only, after a "-" when the provider's negative
    /// sign is "-", and few enough digits that the value cannot overflow. <see langword="false"/> for anything
    /// else, and the caller falls back to the BCL, which reads such text the same way in every culture.
    /// </summary>
    internal static class FastInteger
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse(ReadOnlySpan<byte> s, bool allowMinus, int maxDigits, out long value)
        {
            value = 0;
            bool negative = allowMinus && !s.IsEmpty && s[0] == (byte)'-';
            if (negative)
            {
                s = s[1..];
            }
            if (s.IsEmpty || s.Length > maxDigits)
            {
                return false;
            }
            ulong accumulated = 0;
            if (FastDouble.ReadDigits(s, 0, ref accumulated) != s.Length)
            {
                return false;
            }
            value = negative ? -(long)accumulated : (long)accumulated;
            return true;
        }

        /// <summary>
        /// Writes <paramref name="value"/> right-aligned into the whole of <paramref name="slice"/> in one pass, as
        /// the default format and <see cref="ColumnFormatOptions.AlignInPlace"/> would together: digits from the
        /// right, then <paramref name="padding"/>, with "-" ahead of the padding unless it is a space.
        /// <see langword="false"/> when it does not fit, leaving <paramref name="slice"/> partly written.
        /// </summary>
        public static bool TryFormatRight(long value, Span<byte> slice, byte padding)
        {
            // Unsigned, so long.MinValue negates.
            ulong magnitude = value < 0 ? 0 - (ulong)value : (ulong)value;
            int i = slice.Length;
            do
            {
                if (i == 0)
                {
                    return false;
                }
                ulong quotient = magnitude / 10;
                slice[--i] = (byte)('0' + (magnitude - (quotient * 10)));
                magnitude = quotient;
            }
            while (magnitude != 0);
            if (value >= 0)
            {
                slice[..i].Fill(padding);
                return true;
            }
            if (i == 0)
            {
                return false;
            }
            if (padding == (byte)' ')
            {
                slice[--i] = (byte)'-';
                slice[..i].Fill(padding);
                return true;
            }
            slice[0] = (byte)'-';
            slice[1..i].Fill(padding);
            return true;
        }
    }
}
