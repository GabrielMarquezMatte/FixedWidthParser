using System.Buffers;
using System.Runtime.CompilerServices;
using FixedWidthParser.Processors;

namespace FixedWidthParser.Formatters
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instantiated via reflection (Activator.CreateInstance) in FixedWidthWriter.CreateFormatter.")]
    internal sealed class ConverterColumnFormatter<TModel, TProperty, TConverter>(
        int start, int length, ColumnFormatOptions options, string columnName, RefGetter<TModel, TProperty> getter, TConverter converter)
        : IColumnFormatter<TModel> where TConverter : IFixedWidthConverter<TProperty>
    {
        [SkipLocalsInit]
        public void Format(in TModel model, Span<char> buffer, IFormatProvider? formatProvider)
        {
            var slice = buffer.Slice(start, length);
            var value = getter(in model);

            // Mirrors SpanFormattableColumnFormatter: try a stack buffer first, then grow via
            // ArrayPool (bounded) for the rare converter that needs more room.
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
                $"Value of type \"{typeof(TProperty)}\" for column \"{columnName}\" could not be formatted by " +
                $"converter \"{typeof(TConverter)}\" within {maxSize} characters.");
        }
    }
}
