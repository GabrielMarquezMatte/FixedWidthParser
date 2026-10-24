namespace FixedWidthParser
{
    /// <summary>Assigns a parsed column value to its member of <paramref name="model"/>.</summary>
    public delegate void ColumnSetter<TModel, in TValue>(ref TModel model, TValue value) where TModel : allows ref struct;

    /// <summary>Reads the value a column is written from.</summary>
    public delegate TValue ColumnGetter<TModel, out TValue>(in TModel model) where TModel : allows ref struct;
}
