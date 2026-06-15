using System.Buffers;
using System.Runtime.CompilerServices;

namespace FixedWidthParser.Formatters
{
    public sealed class SpanFormattableColumnFormatter<TModel, TProperty>(
        int start, int length, ColumnFormatOptions options, string columnName, RefGetter<TModel, TProperty> getter)
        : IColumnFormatter<TModel> where TProperty : ISpanFormattable
    {
        [SkipLocalsInit]
        public void Format(in TModel model, Span<char> buffer, IFormatProvider? formatProvider)
        {
            var slice = buffer.Slice(start, length);
            var value = getter(in model);

            // Format into a temporary buffer, then place it in the column with alignment/padding.
            // Covers typical numeric/date formats; longer output falls back to the ArrayPool loop.
            Span<char> stack = stackalloc char[64];
            if (value.TryFormat(stack, out int written, options.Format, formatProvider))
            {
                options.WriteInto(stack[..written], slice, columnName);
                return;
            }

            // Rare: the formatted text exceeds the stack buffer; grow via ArrayPool until it fits.
            // Bounded so a value that never fits (e.g. a misbehaving ISpanFormattable that always
            // returns false) fails fast with a clear error instead of overflowing the size counter
            // into a negative/huge rent. Output is truncated to the column width anyway, so 1M chars
            // is far beyond any real numeric/date/custom format that would ever need to grow.
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
                $"Value of type \"{typeof(TProperty)}\" for column \"{columnName}\" could not be formatted " +
                $"within {maxSize} characters (format \"{options.Format}\").");
        }
    }
}
