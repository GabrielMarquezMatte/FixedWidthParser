namespace FixedWidthParser
{
    internal delegate void RefAction<TModel, in TProperty>(ref TModel model, TProperty value) where TModel : allows ref struct;
    internal delegate TProperty RefGetter<TModel, out TProperty>(in TModel model) where TModel : allows ref struct;
}
