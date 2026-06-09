namespace FixedWidthParser
{
    public delegate void RefAction<TModel, in TProperty>(ref TModel model, TProperty value) where TModel : allows ref struct;
    public delegate TProperty RefGetter<TModel, out TProperty>(in TModel model) where TModel : allows ref struct;
}