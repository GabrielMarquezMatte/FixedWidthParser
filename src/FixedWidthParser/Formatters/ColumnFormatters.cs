using FixedWidthParser.Processors;

namespace FixedWidthParser.Formatters
{
    /// <summary>Formats one column of a model into its slice of the line, for the runtime writer.</summary>
    internal interface IColumnFormatter<TModel>
    {
        void Format(in TModel model, Span<byte> line, in FixedWidthContext context);
    }

    // The formatters are instantiated via reflection (Activator.CreateInstance) in FixedWidthWriter.
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    internal sealed class StringColumnFormatter<TModel>(
        int start, int length, ColumnFormatOptions options, string columnName, RefGetter<TModel, string> getter)
        : IColumnFormatter<TModel>
    {
        public void Format(in TModel model, Span<byte> line, in FixedWidthContext context)
        {
            FixedWidthRuntime.FormatString(getter(in model), line.Slice(start, length), in context, options, columnName);
        }
    }

    internal sealed class SpanFormattableColumnFormatter<TModel, TProperty>(
        int start, int length, ColumnFormatOptions options, string columnName, RefGetter<TModel, TProperty> getter)
        : IColumnFormatter<TModel> where TProperty : ISpanFormattable
    {
        public void Format(in TModel model, Span<byte> line, in FixedWidthContext context)
        {
            FixedWidthRuntime.FormatValue(getter(in model), line.Slice(start, length), in context, options, columnName);
        }
    }

    internal sealed class Utf8SpanFormattableColumnFormatter<TModel, TProperty>(
        int start, int length, ColumnFormatOptions options, string columnName, RefGetter<TModel, TProperty> getter)
        : IColumnFormatter<TModel> where TProperty : IUtf8SpanFormattable, ISpanFormattable
    {
        public void Format(in TModel model, Span<byte> line, in FixedWidthContext context)
        {
            FixedWidthRuntime.FormatUtf8Value(getter(in model), line.Slice(start, length), in context, options, columnName);
        }
    }

    internal sealed class ConverterColumnFormatter<TModel, TProperty, TConverter>(
        int start, int length, ColumnFormatOptions options, string columnName, RefGetter<TModel, TProperty> getter, TConverter converter)
        : IColumnFormatter<TModel> where TConverter : IFixedWidthConverter<TProperty>
    {
        public void Format(in TModel model, Span<byte> line, in FixedWidthContext context)
        {
            FixedWidthRuntime.FormatConvert(getter(in model), line.Slice(start, length), in context, converter, options, columnName);
        }
    }

    /// <summary>A <c>T?</c> column: <see langword="null"/> fills the column with its trim character; a value formats through the <c>T</c> formatter.</summary>
    internal sealed class NullableColumnFormatter<TModel, TUnderlying>(
        int start, int length, byte fill, RefGetter<TModel, TUnderlying?> getter, IColumnFormatter<TModel> inner)
        : IColumnFormatter<TModel> where TUnderlying : struct
    {
        public void Format(in TModel model, Span<byte> line, in FixedWidthContext context)
        {
            if (!getter(in model).HasValue)
            {
                line.Slice(start, length).Fill(fill);
                return;
            }
            inner.Format(in model, line, in context);
        }
    }
#pragma warning restore CA1812
}
