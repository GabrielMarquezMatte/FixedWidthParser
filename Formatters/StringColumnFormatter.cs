namespace Benchmarks.Formatters
{
    public sealed class StringColumnFormatter<TModel>(int start, int length, RefGetter<TModel, string> getter) : IColumnFormatter<TModel>
    {
        public void Format(in TModel model, Span<char> buffer, IFormatProvider? formatProvider)
        {
            var value = getter(in model);
            if (string.IsNullOrEmpty(value)) return;
            var slice = buffer.Slice(start, length);
            int charsToCopy = Math.Min(value.Length, length);
            value.AsSpan(0, charsToCopy).CopyTo(slice);
        }
    }
}