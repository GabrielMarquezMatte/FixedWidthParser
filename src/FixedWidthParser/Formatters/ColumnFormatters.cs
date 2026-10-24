using FixedWidthParser.Infrastructure;

namespace FixedWidthParser.Formatters
{
    /// <summary>Formats one column of a model into its slice of the line, for the runtime writer.</summary>
    internal interface IColumnFormatter<TModel>
        where TModel : allows ref struct
    {
        void Format(in TModel model, Span<byte> line, in FixedWidthContext context);
    }

    // The attribute path instantiates the formatters through reflection (Activator.CreateInstance).
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    internal sealed class StringColumnFormatter<TModel>(
        int start, int length, ColumnFormatOptions options, string columnName, ColumnGetter<TModel, string> getter)
        : IColumnFormatter<TModel> where TModel : allows ref struct
    {
        public void Format(in TModel model, Span<byte> line, in FixedWidthContext context)
        {
            FixedWidthRuntime.FormatString(getter(in model), line.Slice(start, length), in context, options, columnName);
        }
    }

    internal sealed class SpanFormattableColumnFormatter<TModel, TProperty>(
        int start, int length, ColumnFormatOptions options, string columnName, ColumnGetter<TModel, TProperty> getter)
        : IColumnFormatter<TModel> where TProperty : ISpanFormattable
        where TModel : allows ref struct
    {
        public void Format(in TModel model, Span<byte> line, in FixedWidthContext context)
        {
            FixedWidthRuntime.FormatValue(getter(in model), line.Slice(start, length), in context, options, columnName);
        }
    }

    internal sealed class Utf8SpanFormattableColumnFormatter<TModel, TProperty>(
        int start, int length, ColumnFormatOptions options, string columnName, ColumnGetter<TModel, TProperty> getter)
        : IColumnFormatter<TModel> where TProperty : IUtf8SpanFormattable, ISpanFormattable
        where TModel : allows ref struct
    {
        public void Format(in TModel model, Span<byte> line, in FixedWidthContext context)
        {
            FixedWidthRuntime.FormatUtf8Value(getter(in model), line.Slice(start, length), in context, options, columnName);
        }
    }

    internal sealed class ConverterColumnFormatter<TModel, TProperty, TConverter>(
        int start, int length, ColumnFormatOptions options, string columnName, ColumnGetter<TModel, TProperty> getter, TConverter converter)
        : IColumnFormatter<TModel> where TConverter : IFixedWidthConverter<TProperty>
        where TModel : allows ref struct
    {
        public void Format(in TModel model, Span<byte> line, in FixedWidthContext context)
        {
            FixedWidthRuntime.FormatConvert(getter(in model), line.Slice(start, length), in context, converter, options, columnName);
        }
    }

    /// <summary>A <c>T?</c> column: <see langword="null"/> fills the column with its trim character; a value formats through the <c>T</c> formatter.</summary>
    internal sealed class NullableColumnFormatter<TModel, TUnderlying>(
        int start, int length, byte fill, ColumnGetter<TModel, TUnderlying?> getter, IColumnFormatter<TModel> inner)
        : IColumnFormatter<TModel> where TUnderlying : struct
        where TModel : allows ref struct
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
