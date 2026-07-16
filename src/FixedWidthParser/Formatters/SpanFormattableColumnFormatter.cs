namespace FixedWidthParser.Formatters
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instantiated via reflection (Activator.CreateInstance) in FixedWidthWriter.CreateFormatter.")]
    internal sealed class SpanFormattableColumnFormatter<TModel, TProperty>(
        int start, int length, ColumnFormatOptions options, string columnName, RefGetter<TModel, TProperty> getter)
        : IColumnFormatter<TModel> where TProperty : ISpanFormattable
    {
        public void Format(in TModel model, Span<char> buffer, IFormatProvider? formatProvider)
        {
            FixedWidthRuntime.FormatValue(getter(in model), buffer.Slice(start, length), formatProvider, options, columnName);
        }
    }
}
