using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance.Buffers;
using csFastFloat;
using FixedWidthParser.Processors;

namespace FixedWidthParser
{
    /// <summary>
    /// Low-level helpers called by the source-generated <see cref="IFixedWidthModel{TSelf}.TryParse"/>
    /// implementations. They centralize the column slicing, string pooling and numeric parsing so the
    /// generated code stays small and matches the runtime parser's semantics exactly.
    /// <para>Public because the generated code lives in the consumer assembly; not intended for direct use.</para>
    /// </summary>
    public static class FixedWidthRuntime
    {
        /// <summary>Materializes a string column: trims trailing spaces and interns via the pool when supplied.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string String(ReadOnlySpan<char> column, StringPool? stringPool)
        {
            var trimmed = column.TrimEnd(' ');
            return stringPool is null ? trimmed.ToString() : stringPool.GetOrAdd(trimmed);
        }

        /// <summary>Parses a <see cref="double"/> column via csFastFloat, honoring the provider's decimal separator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDouble(ReadOnlySpan<char> column, IFormatProvider? formatProvider, out double value)
            => FastDoubleParser.TryParseDouble(column, out value, decimal_separator: CultureHelpers.GetDecimalSeparator(formatProvider));

        /// <summary>Parses a <see cref="float"/> column via csFastFloat, honoring the provider's decimal separator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFloat(ReadOnlySpan<char> column, IFormatProvider? formatProvider, out float value)
            => FastFloatParser.TryParseFloat(column, out value, decimal_separator: CultureHelpers.GetDecimalSeparator(formatProvider));

        /// <summary>Parses any <see cref="ISpanParsable{TSelf}"/> column (int, decimal, DateTime, …), trimming trailing spaces.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse<TValue>(ReadOnlySpan<char> column, IFormatProvider? formatProvider, [MaybeNullWhen(false)] out TValue value)
            where TValue : ISpanParsable<TValue>
            => TValue.TryParse(column.TrimEnd(' '), formatProvider, out value);
    }
}
