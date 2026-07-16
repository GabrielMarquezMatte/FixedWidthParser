using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Formatters;
using FixedWidthParser.Processors;

namespace FixedWidthParser
{
    /// <summary>
    /// Low-level helpers called by the source-generated <see cref="IFixedWidthModel{TSelf}.TryParse"/>
    /// and <see cref="IFixedWidthModel{TSelf}.TryFormat"/> implementations. They centralize the column
    /// slicing, string pooling, numeric parsing/formatting so the generated code stays small and
    /// matches the runtime parser/writer's semantics exactly.
    /// <para>Public because the generated code lives in the consumer assembly; not intended for direct use.</para>
    /// </summary>
    public static class FixedWidthRuntime
    {
        /// <summary>Materializes a string column: trims trailing spaces and interns via the pool when supplied.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string String(ReadOnlySpan<char> column, StringPool? stringPool, char trimChar = ' ')
        {
            var trimmed = column.TrimEnd(trimChar);
            return stringPool is null ? trimmed.ToString() : stringPool.GetOrAdd(trimmed);
        }

        /// <summary>Parses a <see cref="double"/> column, honoring the provider's decimal separator (see <see cref="CultureHelpers"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDouble(ReadOnlySpan<char> column, IFormatProvider? formatProvider, out double value, char trimChar = ' ')
        {
            return CultureHelpers.TryParseDouble(column, formatProvider, out value, trimChar);
        }

        /// <summary>Parses a <see cref="float"/> column, honoring the provider's decimal separator (see <see cref="CultureHelpers"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFloat(ReadOnlySpan<char> column, IFormatProvider? formatProvider, out float value, char trimChar = ' ')
        {
            return CultureHelpers.TryParseFloat(column, formatProvider, out value, trimChar);
        }

        /// <summary>
        /// Parses any <see cref="ISpanParsable{TSelf}"/> column (int, decimal, DateTime, …), trimming
        /// trailing spaces. A <see langword="null"/> <paramref name="formatProvider"/> means
        /// <see cref="CultureInfo.InvariantCulture"/> (not the BCL's own default of
        /// <see cref="CultureInfo.CurrentCulture"/>) — a fixed-width file is a machine layout, and this
        /// keeps every column type in a record agreeing on what "no provider" means, matching the
        /// double/float columns (see <see cref="Processors.CultureHelpers"/>), which already treat null
        /// as invariant.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse<TValue>(ReadOnlySpan<char> column, IFormatProvider? formatProvider, [MaybeNullWhen(false)] out TValue value, char trimChar = ' ')
            where TValue : ISpanParsable<TValue>
        {
            return TValue.TryParse(column.TrimEnd(trimChar), formatProvider ?? CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Parses a column via a <c>FixedColumnAttribute.Converter</c> instance, trimming trailing spaces.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryConvert<TValue, TConverter>(
            ReadOnlySpan<char> column, IFormatProvider? formatProvider, TConverter converter, [MaybeNullWhen(false)] out TValue value, char trimChar = ' ')
            where TConverter : IFixedWidthConverter<TValue>
        {
            return converter.TryParse(column.TrimEnd(trimChar), formatProvider, out value);
        }

        /// <summary>Formats a string column: fills <paramref name="slice"/> per <paramref name="options"/> (alignment/padding/overflow).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FormatString(string value, Span<char> slice, ColumnFormatOptions options, string columnName)
        {
            options.WriteInto(value.AsSpan(), slice, columnName);
        }

        /// <summary>
        /// Formats an <see cref="ISpanFormattable"/> column into <paramref name="slice"/> per
        /// <paramref name="options"/>: tries a stack buffer first, then grows via a bounded
        /// <see cref="ArrayPool{T}"/> loop for the rare value that needs more room.
        /// </summary>
        [SkipLocalsInit]
        public static void FormatValue<TValue>(TValue value, Span<char> slice, IFormatProvider? formatProvider, ColumnFormatOptions options, string columnName)
            where TValue : ISpanFormattable
        {
            // Null means invariant here too (see TryParse<TValue> above) — otherwise a null-provider
            // write would use CurrentCulture while a null-provider parse of that same value uses
            // invariant, breaking the default round-trip on any non-invariant machine.
            formatProvider ??= CultureInfo.InvariantCulture;
            Span<char> stack = stackalloc char[64];
            if (value.TryFormat(stack, out int written, options.Format, formatProvider))
            {
                options.WriteInto(stack[..written], slice, columnName);
                return;
            }

            const int maxSize = 1 << 20;
            for (int size = 512; size <= maxSize; size *= 2)
            {
                char[] rented = ArrayPool<char>.Shared.Rent(size);
                try
                {
                    if (value.TryFormat(rented, out int w, options.Format, formatProvider))
                    {
                        options.WriteInto(rented.AsSpan(0, w), slice, columnName);
                        return;
                    }
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(rented);
                }
            }

            throw new InvalidOperationException(
                $"Value of type \"{typeof(TValue)}\" for column \"{columnName}\" could not be formatted " +
                $"within {maxSize} characters (format \"{options.Format}\").");
        }

        /// <summary>
        /// Formats a column via a <c>FixedColumnAttribute.Converter</c> instance into <paramref name="slice"/>
        /// per <paramref name="options"/>, with the same stack-then-ArrayPool growth as <see cref="FormatValue{TValue}"/>.
        /// </summary>
        [SkipLocalsInit]
        public static void FormatConvert<TValue, TConverter>(
            TValue value, Span<char> slice, IFormatProvider? formatProvider, TConverter converter, ColumnFormatOptions options, string columnName)
            where TConverter : IFixedWidthConverter<TValue>
        {
            Span<char> stack = stackalloc char[64];
            if (converter.TryFormat(value, stack, formatProvider, out int written))
            {
                options.WriteInto(stack[..written], slice, columnName);
                return;
            }

            const int maxSize = 1 << 20;
            for (int size = 512; size <= maxSize; size *= 2)
            {
                char[] rented = ArrayPool<char>.Shared.Rent(size);
                try
                {
                    if (converter.TryFormat(value, rented, formatProvider, out int w))
                    {
                        options.WriteInto(rented.AsSpan(0, w), slice, columnName);
                        return;
                    }
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(rented);
                }
            }

            throw new InvalidOperationException(
                $"Value of type \"{typeof(TValue)}\" for column \"{columnName}\" could not be formatted by " +
                $"converter \"{typeof(TConverter)}\" within {maxSize} characters.");
        }
    }
}
