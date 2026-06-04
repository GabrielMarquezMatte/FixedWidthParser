using System.Globalization;
using System.Runtime.CompilerServices;

namespace FixedWidthParser.Processors
{
    internal static class CultureHelpers
    {
        // Single-entry memo: the decimal separator depends only on the IFormatProvider, which is
        // fixed during a read. The holder is immutable and swapped by reference (atomic), so reads
        // and writes are thread-safe — a race only recomputes. Allocates once per distinct provider.
        private sealed class Memo(IFormatProvider provider, char separator)
        {
            public readonly IFormatProvider Provider = provider;
            public readonly char Separator = separator;
        }

        private static Memo? _memo;

        /// <summary>
        /// Decimal separator derived from the IFormatProvider (dot when null). Used by the
        /// double/float processors, which pass the character on to csFastFloat. Memoized per provider
        /// so it is not re-derived for every numeric column on every line.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char GetDecimalSeparator(IFormatProvider? formatProvider)
        {
            if (formatProvider is null)
            {
                return '.';
            }
            var memo = _memo;
            if (memo is not null && ReferenceEquals(memo.Provider, formatProvider))
            {
                return memo.Separator;
            }
            return Resolve(formatProvider);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static char Resolve(IFormatProvider formatProvider)
        {
            char separator = NumberFormatInfo.GetInstance(formatProvider).NumberDecimalSeparator[0];
            _memo = new Memo(formatProvider, separator);
            return separator;
        }
    }
}
