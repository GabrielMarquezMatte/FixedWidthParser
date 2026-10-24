using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using FixedWidthParser.Infrastructure;
using csFastFloat;

namespace FixedWidthParser.Processors
{
    /// <summary>
    /// Culture-aware <c>double</c>/<c>float</c> parsing. csFastFloat is trusted only when the provider's
    /// decimal separator is one ASCII character and its negative sign is "-"
    /// (<see cref="FixedWidthContext.DecimalSeparator"/>), and even then only after checking every byte of the
    /// (trimmed) field was consumed: csFastFloat does not fail on trailing content it doesn't recognize, it
    /// silently stops and returns the leading prefix it did parse. Any other provider, and a value using
    /// thousands separators, falls back to the <see cref="NumberFormatInfo"/>-aware BCL parser over the decoded text.
    /// </summary>
    internal static class CultureHelpers
    {
        /// <summary>Parses a (not yet trimmed) column as <see cref="double"/>, honoring the context's provider.</summary>
        public static bool TryParseDouble(ReadOnlySpan<byte> column, in FixedWidthContext context, in ColumnParseOptions options, out double value)
        {
            // Also trim leading spaces: csFastFloat's characters_consumed does not reliably include
            // skipped leading whitespace, so a right-aligned column would otherwise fail the
            // full-consumption check below.
            var trimmed = FixedWidthRuntime.TrimColumn(column, in options).TrimStart((byte)' ');
            byte separator = context.DecimalSeparator;
            if (separator != 0
                && FastDoubleParser.TryParseDouble(trimmed, out int consumed, out value, decimal_separator: separator)
                && consumed == trimmed.Length)
            {
                return true;
            }
            return TryParseDecoded(trimmed, context.FormatProvider, context.Encoding, out value);
        }

        /// <summary>Parses a (not yet trimmed) column as <see cref="float"/>, honoring the context's provider.</summary>
        public static bool TryParseFloat(ReadOnlySpan<byte> column, in FixedWidthContext context, in ColumnParseOptions options, out float value)
        {
            var trimmed = FixedWidthRuntime.TrimColumn(column, in options).TrimStart((byte)' ');
            byte separator = context.DecimalSeparator;
            if (separator != 0
                && FastFloatParser.TryParseFloat(trimmed, out int consumed, out value, decimal_separator: separator)
                && consumed == trimmed.Length)
            {
                return true;
            }
            return TryParseDecoded(trimmed, context.FormatProvider, context.Encoding, out value);
        }

        [SkipLocalsInit]
        private static bool TryParseDecoded<TValue>(ReadOnlySpan<byte> field, IFormatProvider formatProvider, Encoding encoding, [MaybeNullWhen(false)] out TValue value)
            where TValue : INumberBase<TValue>
        {
            const NumberStyles styles = NumberStyles.Float | NumberStyles.AllowThousands;
            int max = encoding.GetMaxCharCount(field.Length);
            if (max <= 128)
            {
                Span<char> chars = stackalloc char[128];
                int written = encoding.GetChars(field, chars);
                return TValue.TryParse(chars[..written], styles, formatProvider, out value);
            }
            char[] rented = ArrayPool<char>.Shared.Rent(max);
            try
            {
                int written = encoding.GetChars(field, rented);
                return TValue.TryParse(rented.AsSpan(0, written), styles, formatProvider, out value);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }
}
