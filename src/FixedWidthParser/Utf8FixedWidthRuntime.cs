using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using csFastFloat;
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
        /// <summary>Materializes a string column: trims trailing spaces and UTF-8 decodes, interning via the pool when supplied.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string String(ReadOnlySpan<byte> column, StringPool? stringPool)
        {
            var trimmed = column.TrimEnd((byte)' ');
            return stringPool is null ? Encoding.UTF8.GetString(trimmed) : stringPool.GetOrAdd(trimmed, Encoding.UTF8);
        }

        /// <summary>Parses a <see cref="double"/> column via csFastFloat, honoring the provider's decimal separator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDouble(ReadOnlySpan<byte> column, IFormatProvider? formatProvider, out double value)
        {
            return FastDoubleParser.TryParseDouble(column, out value, decimal_separator: (byte)CultureHelpers.GetDecimalSeparator(formatProvider));
        }


        /// <summary>Parses a <see cref="float"/> column via csFastFloat, honoring the provider's decimal separator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFloat(ReadOnlySpan<byte> column, IFormatProvider? formatProvider, out float value)
        {
            return FastFloatParser.TryParseFloat(column, out value, decimal_separator: (byte)CultureHelpers.GetDecimalSeparator(formatProvider));
        }


        /// <summary>Parses any <see cref="IUtf8SpanParsable{TSelf}"/> column (int, decimal, DateTime, …), trimming trailing spaces.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse<TValue>(ReadOnlySpan<byte> column, IFormatProvider? formatProvider, [MaybeNullWhen(false)] out TValue value)
            where TValue : IUtf8SpanParsable<TValue>
        {
            return TValue.TryParse(column.TrimEnd((byte)' '), formatProvider, out value);
        }

    }
}
