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
    /// Culture-aware <c>double</c>/<c>float</c> parsing. csFastFloat is only trusted for the '.' decimal
    /// separator, and even then only after checking every byte of the (trimmed) field was consumed —
    /// csFastFloat does not fail on trailing content it doesn't recognize, it silently stops and returns
    /// the leading prefix it did parse. Any other separator, and a dot-culture value using thousands
    /// separators, falls back to the <see cref="NumberFormatInfo"/>-aware BCL parser over the decoded text.
    /// </summary>
    internal static class CultureHelpers
    {
        // Decimal separator cache keyed by IFormatProvider identity. A ConditionalWeakTable ties each
        // entry's lifetime to its provider's own lifetime: no manual eviction, no unbounded growth if a
        // caller passes many distinct (e.g. per-request) providers, and — unlike a single-entry memo —
        // no thrash when two providers are used alternately in the same process.
        private static readonly ConditionalWeakTable<IFormatProvider, StrongBox<char>> _separatorCache = new();

        private sealed class CacheEntry(IFormatProvider provider, char separator)
        {
            public readonly IFormatProvider Provider = provider;
            public readonly char Separator = separator;
        }
        private static CacheEntry? _cache;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char GetDecimalSeparator(IFormatProvider formatProvider)
        {
            var cache = _cache;
            if (cache is not null && ReferenceEquals(formatProvider, cache.Provider))
            {
                return cache.Separator;
            }
            char separator = _separatorCache.GetValue(formatProvider, static fp => new StrongBox<char>(Resolve(fp))).Value;
            _cache = new CacheEntry(formatProvider, separator);
            return separator;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static char Resolve(IFormatProvider formatProvider)
        {
            return NumberFormatInfo.GetInstance(formatProvider).NumberDecimalSeparator[0];
        }

        /// <summary>Parses a (not yet trimmed) column as <see cref="double"/>, honoring the context's provider.</summary>
        public static bool TryParseDouble(ReadOnlySpan<byte> column, in FixedWidthContext context, in ColumnParseOptions options, out double value)
        {
            // Also trim leading spaces: csFastFloat's characters_consumed does not reliably include
            // skipped leading whitespace, so a right-aligned column would otherwise fail the
            // full-consumption check below.
            var trimmed = FixedWidthRuntime.TrimColumn(column, in options).TrimStart((byte)' ');
            var formatProvider = context.FormatProvider;
            if (GetDecimalSeparator(formatProvider) == '.'
                && FastDoubleParser.TryParseDouble(trimmed, out int consumed, out value, decimal_separator: (byte)'.')
                && consumed == trimmed.Length)
            {
                return true;
            }
            return TryParseDecoded(trimmed, formatProvider, context.Encoding, out value);
        }

        /// <summary>Parses a (not yet trimmed) column as <see cref="float"/>, honoring the context's provider.</summary>
        public static bool TryParseFloat(ReadOnlySpan<byte> column, in FixedWidthContext context, in ColumnParseOptions options, out float value)
        {
            var trimmed = FixedWidthRuntime.TrimColumn(column, in options).TrimStart((byte)' ');
            var formatProvider = context.FormatProvider;
            if (GetDecimalSeparator(formatProvider) == '.'
                && FastFloatParser.TryParseFloat(trimmed, out int consumed, out value, decimal_separator: (byte)'.')
                && consumed == trimmed.Length)
            {
                return true;
            }
            return TryParseDecoded(trimmed, formatProvider, context.Encoding, out value);
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
