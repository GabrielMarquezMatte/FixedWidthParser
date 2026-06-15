namespace FixedWidthParser.Formatters
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instantiated via reflection (Activator.CreateInstance) in FixedWidthWriter.CreateFormatter.")]
    internal sealed class StringColumnFormatter<TModel>(
        int start, int length, ColumnFormatOptions options, string columnName, RefGetter<TModel, string> getter)
        : IColumnFormatter<TModel>
    {
        public void Format(in TModel model, Span<char> buffer, IFormatProvider? formatProvider)
        {
            var slice = buffer.Slice(start, length);
            var value = getter(in model);
            options.WriteInto(value.AsSpan(), slice, columnName);
        }
    }
}
