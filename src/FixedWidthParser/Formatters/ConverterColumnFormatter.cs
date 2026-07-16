using FixedWidthParser.Processors;

namespace FixedWidthParser.Formatters
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instantiated via reflection (Activator.CreateInstance) in FixedWidthWriter.CreateFormatter.")]
    internal sealed class ConverterColumnFormatter<TModel, TProperty, TConverter>(
        int start, int length, ColumnFormatOptions options, string columnName, RefGetter<TModel, TProperty> getter, TConverter converter)
        : IColumnFormatter<TModel> where TConverter : IFixedWidthConverter<TProperty>
    {
        public void Format(in TModel model, Span<char> buffer, IFormatProvider? formatProvider)
        {
            FixedWidthRuntime.FormatConvert(getter(in model), buffer.Slice(start, length), formatProvider, converter, options, columnName);
        }
    }
}
