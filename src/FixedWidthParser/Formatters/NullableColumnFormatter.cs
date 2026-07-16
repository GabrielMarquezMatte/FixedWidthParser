namespace FixedWidthParser.Formatters
{
    /// <summary>
    /// Wraps another <see cref="IColumnFormatter{TModel}"/> (built for the underlying <c>T</c> of a
    /// <c>T?</c> column) so a <see langword="null"/> value writes as a blank (padding-filled) column
    /// instead of delegating to the inner formatter — no duplicated formatting logic per type.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instantiated via reflection (Activator.CreateInstance) in FixedWidthWriter.CreateFormatter.")]
    internal sealed class NullableColumnFormatter<TModel, TUnderlying>(
        int start, int length, ColumnFormatOptions options, RefGetter<TModel, TUnderlying?> getter, IColumnFormatter<TModel> inner)
        : IColumnFormatter<TModel> where TUnderlying : struct
    {
        public void Format(in TModel model, Span<char> buffer, IFormatProvider? formatProvider)
        {
            if (!getter(in model).HasValue)
            {
                buffer.Slice(start, length).Fill(options.Padding);
                return;
            }
            inner.Format(in model, buffer, formatProvider);
        }
    }
}
