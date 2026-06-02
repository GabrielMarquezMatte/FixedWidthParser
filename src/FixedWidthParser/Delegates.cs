namespace FixedWidthParser
{
    public delegate void RefAction<TModel, TProperty>(ref TModel model, TProperty value) where TModel : allows ref struct;
    public delegate TProperty RefGetter<TModel, TProperty>(in TModel model) where TModel : allows ref struct;
}