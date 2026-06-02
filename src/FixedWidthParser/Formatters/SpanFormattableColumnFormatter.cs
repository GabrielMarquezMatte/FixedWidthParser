namespace FixedWidthParser.Formatters
{
    public sealed class SpanFormattableColumnFormatter<TModel, TProperty>(int start, int length, RefGetter<TModel, TProperty> getter) : IColumnFormatter<TModel> where TProperty : ISpanFormattable
    {
        public void Format(in TModel model, Span<char> buffer, IFormatProvider? formatProvider)
        {
            var value = getter(in model);
            var slice = buffer.Slice(start, length);
            value.TryFormat(slice, out _, default, formatProvider);
        }
    }
}