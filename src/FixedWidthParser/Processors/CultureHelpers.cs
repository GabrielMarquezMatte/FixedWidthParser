using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using csFastFloat;

namespace FixedWidthParser.Processors
{
    /// <summary>
    /// Culture-aware <c>double</c>/<c>float</c> parsing for the double/float column builders (char and
    /// UTF-8). csFastFloat is only trusted for the invariant '.' decimal separator, and even then only
    /// after checking every character of the (trimmed) field was consumed — csFastFloat does not fail
    /// on trailing content it doesn't recognize, it silently stops and returns the leading prefix it
    /// did parse (so e.g. a thousands separator it wasn't told about would otherwise truncate "1.234,56"
    /// down to "1" and still report success). Any other separator falls back to the real,
    /// <see cref="NumberFormatInfo"/>-aware <see cref="double.TryParse(ReadOnlySpan{char},NumberStyles,IFormatProvider?,out double)"/>,
    /// which csFastFloat's single <c>decimal_separator</c> override cannot express correctly (thousands
    /// separators, sign placement, …) and which validates the whole input natively.
    /// </summary>
    internal static class CultureHelpers
    {
        // Decimal separator cache keyed by IFormatProvider identity. A ConditionalWeakTable ties each
        // entry's lifetime to its provider's own lifetime: no manual eviction, no unbounded growth if a
        // caller passes many distinct (e.g. per-request) providers, and — unlike a single-entry memo —
        // no thrash when two providers are used alternately in the same process.
        private static readonly ConditionalWeakTable<IFormatProvider, StrongBox<char>> _separatorCache = new();

        /// <summary>
        /// Decimal separator derived from the IFormatProvider (dot when null), memoized per provider.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char GetDecimalSeparator(IFormatProvider? formatProvider)
        {
            if (formatProvider is null)
            {
                return '.';
            }
            return _separatorCache.GetValue(formatProvider, static fp => new StrongBox<char>(Resolve(fp))).Value;
        }

        /// <summary>
        /// Decimal separator as a single UTF-8 byte, for the byte-based numeric parsers. Only an ASCII
        /// separator (&lt;= 0x7F) can be represented as one byte; a non-ASCII separator would be a
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
            return NumberFormatInfo.GetInstance(formatProvider).NumberDecimalSeparator[0];
        }

        /// <summary>
        /// Parses a (not yet trimmed) column as <see cref="double"/>, honoring <paramref name="formatProvider"/>.
        /// A <see langword="null"/> provider means <see cref="CultureInfo.InvariantCulture"/> — a fixed-width
        /// file is a machine layout, not UI text, so a caller that doesn't pass a provider should not be at
        /// the mercy of the running thread's <see cref="CultureInfo.CurrentCulture"/> (which every other
        /// column type, and this method's own BCL fallback below, would otherwise resolve to).
        /// </summary>
        public static bool TryParseDouble(ReadOnlySpan<char> column, IFormatProvider? formatProvider, out double value, char trimChar = ' ')
        {
            // Also trim leading whitespace: csFastFloat's characters_consumed does not reliably include
            // skipped leading whitespace across its double/float overloads (verified empirically —
            // FastDoubleParser counts it, FastFloatParser doesn't), so a right-aligned (space-padded)
            // column would otherwise fail the full-consumption check below even though the BCL fallback
            // (which does allow leading whitespace) would have accepted it.
            var trimmed = column.TrimEnd(trimChar).TrimStart();
            if (GetDecimalSeparator(formatProvider) == '.')
            {
                if (FastDoubleParser.TryParseDouble(trimmed, out int consumed, out value, decimal_separator: '.')
                    && consumed == trimmed.Length)
                {
                    return true;
                }
                // csFastFloat's single decimal_separator override can't express thousands separators or
                // sign placement; a dot-culture value using them (e.g. "1,234.56") fails the fast path
                // above even though it's valid, so fall back to the real NumberFormatInfo-aware parser,
                // which validates the whole input natively (still rejects genuine garbage like "12x").
                return double.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, formatProvider ?? CultureInfo.InvariantCulture, out value);
            }
            return double.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, formatProvider, out value);
        }

        /// <summary>Parses a (not yet trimmed) column as <see cref="float"/>, honoring <paramref name="formatProvider"/> (see <see cref="TryParseDouble(ReadOnlySpan{char},IFormatProvider?,out double,char)"/> for the null-provider contract).</summary>
        public static bool TryParseFloat(ReadOnlySpan<char> column, IFormatProvider? formatProvider, out float value, char trimChar = ' ')
        {
            var trimmed = column.TrimEnd(trimChar).TrimStart();
            if (GetDecimalSeparator(formatProvider) == '.')
            {
                if (FastFloatParser.TryParseFloat(trimmed, out int consumed, out value, decimal_separator: '.')
                    && consumed == trimmed.Length)
                {
                    return true;
                }
                return float.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, formatProvider ?? CultureInfo.InvariantCulture, out value);
            }
            return float.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, formatProvider, out value);
        }

        /// <summary>
        /// UTF-8 counterpart of <see cref="TryParseDouble(ReadOnlySpan{char},IFormatProvider?,out double,char)"/>.
        /// A non-ASCII separator still throws (see <see cref="GetDecimalSeparatorByte"/>) — the byte path
        /// only supports separators representable as a single UTF-8 byte.
        /// </summary>
        public static bool TryParseDouble(ReadOnlySpan<byte> column, IFormatProvider? formatProvider, out double value, byte trimChar = (byte)' ')
        {
            byte separator = GetDecimalSeparatorByte(formatProvider);
            var trimmed = column.TrimEnd(trimChar).TrimStart((byte)' ');
            if (separator == (byte)'.'
                && FastDoubleParser.TryParseDouble(trimmed, out int consumed, out value, decimal_separator: separator)
                && consumed == trimmed.Length)
            {
                return true;
            }
            return TryParseDoubleViaTranscode(trimmed, formatProvider, out value);
        }

        /// <summary>UTF-8 counterpart of <see cref="TryParseFloat(ReadOnlySpan{char},IFormatProvider?,out float,char)"/>.</summary>
        public static bool TryParseFloat(ReadOnlySpan<byte> column, IFormatProvider? formatProvider, out float value, byte trimChar = (byte)' ')
        {
            byte separator = GetDecimalSeparatorByte(formatProvider);
            var trimmed = column.TrimEnd(trimChar).TrimStart((byte)' ');
            if (separator == (byte)'.'
                && FastFloatParser.TryParseFloat(trimmed, out int consumed, out value, decimal_separator: separator)
                && consumed == trimmed.Length)
            {
                return true;
            }
            return TryParseFloatViaTranscode(trimmed, formatProvider, out value);
        }

        // Non-dot ASCII separators (e.g. ',') still need NumberFormatInfo-aware parsing (thousands
        // separators, sign placement); there is no BCL span-based double.TryParse that both takes an
        // IFormatProvider and operates on UTF-8 bytes, so the (already-trimmed, always-short) numeric
        // field is transcoded to a small char buffer first. Also used as the dot-path's thousands-separator
        // fallback above, so a null provider is normalized to invariant here too (see TryParseDouble's doc).
        [SkipLocalsInit]
        private static bool TryParseDoubleViaTranscode(ReadOnlySpan<byte> field, IFormatProvider? formatProvider, out double value)
        {
            formatProvider ??= CultureInfo.InvariantCulture;
            const int stackThreshold = 128;
            if (field.Length <= stackThreshold)
            {
                Span<char> chars = stackalloc char[field.Length];
                int written = Encoding.UTF8.GetChars(field, chars);
                return double.TryParse(chars[..written], NumberStyles.Float | NumberStyles.AllowThousands, formatProvider, out value);
            }
            char[] rented = ArrayPool<char>.Shared.Rent(field.Length);
            try
            {
                int written = Encoding.UTF8.GetChars(field, rented);
                return double.TryParse(rented.AsSpan(0, written), NumberStyles.Float | NumberStyles.AllowThousands, formatProvider, out value);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }

        [SkipLocalsInit]
        private static bool TryParseFloatViaTranscode(ReadOnlySpan<byte> field, IFormatProvider? formatProvider, out float value)
        {
            formatProvider ??= CultureInfo.InvariantCulture;
            const int stackThreshold = 128;
            if (field.Length <= stackThreshold)
            {
                Span<char> chars = stackalloc char[field.Length];
                int written = Encoding.UTF8.GetChars(field, chars);
                return float.TryParse(chars[..written], NumberStyles.Float | NumberStyles.AllowThousands, formatProvider, out value);
            }
            char[] rented = ArrayPool<char>.Shared.Rent(field.Length);
            try
            {
                int written = Encoding.UTF8.GetChars(field, rented);
                return float.TryParse(rented.AsSpan(0, written), NumberStyles.Float | NumberStyles.AllowThousands, formatProvider, out value);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }
}
