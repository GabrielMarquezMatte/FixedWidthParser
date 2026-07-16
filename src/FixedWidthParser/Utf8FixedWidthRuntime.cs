using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Processors;

namespace FixedWidthParser
{
    /// <summary>
    /// UTF-8 / byte counterpart of <see cref="FixedWidthRuntime"/>: low-level helpers called by the
    /// source-generated <see cref="IUtf8FixedWidthModel{TSelf}.TryParse"/> implementations. They
    /// centralize the column slicing, UTF-8 string decoding/pooling and numeric parsing so the generated
    /// code stays small and matches the runtime byte parser's semantics exactly.
    /// <para>Public because the generated code lives in the consumer assembly; not intended for direct use.</para>
    /// </summary>
    public static class Utf8FixedWidthRuntime
    {
        /// <summary>
        /// Converts a <c>FixedColumnAttribute.TrimChar</c> (or any other single character configured for
        /// the char path) to the single UTF-8 byte it maps to. Only ASCII (&lt;= 0x7F) characters are a
        /// single byte in UTF-8; anything else throws rather than silently trimming the wrong byte.
        /// </summary>
        /// <exception cref="NotSupportedException">The character is not a single ASCII byte.</exception>
        public static byte ToAsciiByte(char value, string columnName)
        {
            if (value > '\x7F')
            {
                throw new NotSupportedException(
                    $"The trim character '{value}' (U+{(int)value:X4}) for column \"{columnName}\" is not a single ASCII " +
                    "byte. The UTF-8 byte parser only supports ASCII trim characters; use the char-based parser for this column.");
            }
            return (byte)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<byte> TrimColumn(ReadOnlySpan<byte> column, byte trimChar, Attributes.TrimMode trimMode)
        {
            return trimMode switch
            {
                Attributes.TrimMode.Leading => column.TrimStart(trimChar),
                Attributes.TrimMode.Both => column.Trim(trimChar),
                _ => column.TrimEnd(trimChar)
            };
        }

        /// <summary>Materializes a string column: trims spaces and UTF-8 decodes, interning via the pool when supplied.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string String(ReadOnlySpan<byte> column, StringPool? stringPool, byte trimChar = (byte)' ', Attributes.TrimMode trimMode = Attributes.TrimMode.Trailing)
        {
            var trimmed = TrimColumn(column, trimChar, trimMode);
            return stringPool is null ? Encoding.UTF8.GetString(trimmed) : stringPool.GetOrAdd(trimmed, Encoding.UTF8);
        }

        /// <summary>Parses a <see cref="double"/> column, honoring the provider's decimal separator (see <see cref="CultureHelpers"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDouble(ReadOnlySpan<byte> column, IFormatProvider? formatProvider, out double value, byte trimChar = (byte)' ', Attributes.TrimMode trimMode = Attributes.TrimMode.Trailing)
        {
            return CultureHelpers.TryParseDouble(column, formatProvider, out value, trimChar, trimMode);
        }

        /// <summary>Parses a <see cref="float"/> column, honoring the provider's decimal separator (see <see cref="CultureHelpers"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFloat(ReadOnlySpan<byte> column, IFormatProvider? formatProvider, out float value, byte trimChar = (byte)' ', Attributes.TrimMode trimMode = Attributes.TrimMode.Trailing)
        {
            return CultureHelpers.TryParseFloat(column, formatProvider, out value, trimChar, trimMode);
        }

        /// <summary>
        /// Parses any <see cref="IUtf8SpanParsable{TSelf}"/> column (int, decimal, DateTime, …), trimming
        /// spaces. A <see langword="null"/> <paramref name="formatProvider"/> means
        /// <see cref="CultureInfo.InvariantCulture"/> — see <see cref="FixedWidthRuntime.TryParse{TValue}"/>
        /// for why (same contract on both the char and UTF-8 paths).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse<TValue>(ReadOnlySpan<byte> column, IFormatProvider? formatProvider, [MaybeNullWhen(false)] out TValue value, byte trimChar = (byte)' ', Attributes.TrimMode trimMode = Attributes.TrimMode.Trailing)
            where TValue : IUtf8SpanParsable<TValue>
        {
            return TValue.TryParse(TrimColumn(column, trimChar, trimMode), formatProvider ?? CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Parses a column via a <c>FixedColumnAttribute.Converter</c> instance, trimming spaces.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryConvert<TValue, TConverter>(
            ReadOnlySpan<byte> column, IFormatProvider? formatProvider, TConverter converter, [MaybeNullWhen(false)] out TValue value, byte trimChar = (byte)' ', Attributes.TrimMode trimMode = Attributes.TrimMode.Trailing)
            where TConverter : IUtf8FixedWidthConverter<TValue>
        {
            return converter.TryParse(TrimColumn(column, trimChar, trimMode), formatProvider, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDateTimeExact(ReadOnlySpan<byte> column, string format, IFormatProvider? formatProvider, out DateTime value, byte trimChar = (byte)' ', Attributes.TrimMode trimMode = Attributes.TrimMode.Trailing)
        {
            var trimmed = TrimColumn(column, trimChar, trimMode);
            Span<char> chars = trimmed.Length <= 128 ? stackalloc char[128] : new char[trimmed.Length];
            int written = Encoding.UTF8.GetChars(trimmed, chars);
            return DateTime.TryParseExact(chars[..written], format, formatProvider ?? CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDateOnlyExact(ReadOnlySpan<byte> column, string format, IFormatProvider? formatProvider, out DateOnly value, byte trimChar = (byte)' ', Attributes.TrimMode trimMode = Attributes.TrimMode.Trailing)
        {
            var trimmed = TrimColumn(column, trimChar, trimMode);
            Span<char> chars = trimmed.Length <= 128 ? stackalloc char[128] : new char[trimmed.Length];
            int written = Encoding.UTF8.GetChars(trimmed, chars);
            return DateOnly.TryParseExact(chars[..written], format, formatProvider ?? CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryTimeOnlyExact(ReadOnlySpan<byte> column, string format, IFormatProvider? formatProvider, out TimeOnly value, byte trimChar = (byte)' ', Attributes.TrimMode trimMode = Attributes.TrimMode.Trailing)
        {
            var trimmed = TrimColumn(column, trimChar, trimMode);
            Span<char> chars = trimmed.Length <= 128 ? stackalloc char[128] : new char[trimmed.Length];
            int written = Encoding.UTF8.GetChars(trimmed, chars);
            return TimeOnly.TryParseExact(chars[..written], format, formatProvider ?? CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDateTimeOffsetExact(ReadOnlySpan<byte> column, string format, IFormatProvider? formatProvider, out DateTimeOffset value, byte trimChar = (byte)' ', Attributes.TrimMode trimMode = Attributes.TrimMode.Trailing)
        {
            var trimmed = TrimColumn(column, trimChar, trimMode);
            Span<char> chars = trimmed.Length <= 128 ? stackalloc char[128] : new char[trimmed.Length];
            int written = Encoding.UTF8.GetChars(trimmed, chars);
            return DateTimeOffset.TryParseExact(chars[..written], format, formatProvider ?? CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
        }
    }
}
