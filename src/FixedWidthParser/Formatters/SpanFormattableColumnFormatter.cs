using System.Buffers;

namespace FixedWidthParser.Formatters
{
    public sealed class SpanFormattableColumnFormatter<TModel, TProperty>(
        int start, int length, ColumnFormatOptions options, string columnName, RefGetter<TModel, TProperty> getter)
        : IColumnFormatter<TModel> where TProperty : ISpanFormattable
    {
        public void Format(in TModel model, Span<char> buffer, IFormatProvider? formatProvider)
        {
            var slice = buffer.Slice(start, length);
            var value = getter(in model);

            // Formata num buffer temporário e então coloca na coluna com alinhamento/preenchimento.
            Span<char> stack = stackalloc char[256];
            if (value.TryFormat(stack, out int written, options.Format, formatProvider))
            {
                options.WriteInto(stack[..written], slice, columnName);
                return;
            }

            // Raro: o texto formatado excede o buffer da pilha; cresce via ArrayPool.
            for (int size = 512; ; size *= 2)
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
        }
    }
}
