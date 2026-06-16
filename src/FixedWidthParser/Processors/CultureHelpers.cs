using System.Diagnostics.CodeAnalysis;
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

        private static volatile Memo? _memo;

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

        /// <summary>
        /// Decimal separator as a single UTF-8 byte, for the byte-based numeric parsers (csFastFloat's
        /// byte API on the UTF-8 path). That API matches the separator against raw bytes, so only an
        /// ASCII separator (&lt;= 0x7F) can be represented as one byte; a non-ASCII separator would be a
        /// multi-byte sequence in the UTF-8 data and is rejected with a clear error rather than silently
        /// truncated to the wrong byte.
        /// </summary>
        /// <exception cref="NotSupportedException">The culture's decimal separator is not a single ASCII character.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetDecimalSeparatorByte(IFormatProvider? formatProvider)
        {
            char separator = GetDecimalSeparator(formatProvider);
            if (separator > '\x7F')
            {
                ThrowNonAsciiSeparator(separator);
            }
            return (byte)separator;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DoesNotReturn]
        private static void ThrowNonAsciiSeparator(char separator)
        {
            throw new NotSupportedException(
                $"The decimal separator '{separator}' (U+{(int)separator:X4}) is not a single ASCII byte. " +
                "The UTF-8 byte parser only supports ASCII decimal separators; use the char-based parser for this culture.");
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
