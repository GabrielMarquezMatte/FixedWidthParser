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
    }
}
