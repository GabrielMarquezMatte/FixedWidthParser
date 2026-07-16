using System.Diagnostics.CodeAnalysis;
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

        /// <summary>Materializes a string column: trims trailing spaces and UTF-8 decodes, interning via the pool when supplied.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string String(ReadOnlySpan<byte> column, StringPool? stringPool, byte trimChar = (byte)' ')
        {
            var trimmed = column.TrimEnd(trimChar);
            return stringPool is null ? Encoding.UTF8.GetString(trimmed) : stringPool.GetOrAdd(trimmed, Encoding.UTF8);
        }

        /// <summary>Parses a <see cref="double"/> column, honoring the provider's decimal separator (see <see cref="CultureHelpers"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDouble(ReadOnlySpan<byte> column, IFormatProvider? formatProvider, out double value, byte trimChar = (byte)' ')
        {
            return CultureHelpers.TryParseDouble(column, formatProvider, out value, trimChar);
        }

        /// <summary>Parses a <see cref="float"/> column, honoring the provider's decimal separator (see <see cref="CultureHelpers"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFloat(ReadOnlySpan<byte> column, IFormatProvider? formatProvider, out float value, byte trimChar = (byte)' ')
        {
            return CultureHelpers.TryParseFloat(column, formatProvider, out value, trimChar);
        }


        /// <summary>Parses any <see cref="IUtf8SpanParsable{TSelf}"/> column (int, decimal, DateTime, …), trimming trailing spaces.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse<TValue>(ReadOnlySpan<byte> column, IFormatProvider? formatProvider, [MaybeNullWhen(false)] out TValue value, byte trimChar = (byte)' ')
            where TValue : IUtf8SpanParsable<TValue>
        {
            return TValue.TryParse(column.TrimEnd(trimChar), formatProvider, out value);
        }

        /// <summary>Parses a column via a <c>FixedColumnAttribute.Converter</c> instance, trimming trailing spaces.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryConvert<TValue, TConverter>(
            ReadOnlySpan<byte> column, IFormatProvider? formatProvider, TConverter converter, [MaybeNullWhen(false)] out TValue value, byte trimChar = (byte)' ')
            where TConverter : IUtf8FixedWidthConverter<TValue>
        {
            return converter.TryParse(column.TrimEnd(trimChar), formatProvider, out value);
        }
    }
}
