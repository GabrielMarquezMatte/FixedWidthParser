using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance.Buffers;
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

        /// <summary>Parses a <see cref="double"/> column, honoring the provider's decimal separator (see <see cref="CultureHelpers"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDouble(ReadOnlySpan<char> column, IFormatProvider? formatProvider, out double value)
        {
            return CultureHelpers.TryParseDouble(column, formatProvider, out value);
        }

        /// <summary>Parses a <see cref="float"/> column, honoring the provider's decimal separator (see <see cref="CultureHelpers"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFloat(ReadOnlySpan<char> column, IFormatProvider? formatProvider, out float value)
        {
            return CultureHelpers.TryParseFloat(column, formatProvider, out value);
        }

        /// <summary>Parses any <see cref="ISpanParsable{TSelf}"/> column (int, decimal, DateTime, …), trimming trailing spaces.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse<TValue>(ReadOnlySpan<char> column, IFormatProvider? formatProvider, [MaybeNullWhen(false)] out TValue value)
            where TValue : ISpanParsable<TValue>
        {
            return TValue.TryParse(column.TrimEnd(' '), formatProvider, out value);
        }

        /// <summary>Parses a column via a <c>FixedColumnAttribute.Converter</c> instance, trimming trailing spaces.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryConvert<TValue, TConverter>(
            ReadOnlySpan<char> column, IFormatProvider? formatProvider, TConverter converter, [MaybeNullWhen(false)] out TValue value)
            where TConverter : IFixedWidthConverter<TValue>
        {
            return converter.TryParse(column.TrimEnd(' '), formatProvider, out value);
        }
    }
}
