namespace FixedWidthParser
{
#if NET9_0_OR_GREATER
    public delegate void RefAction<TModel, in TProperty>(ref TModel model, TProperty value) where TModel : allows ref struct;
    public delegate TProperty RefGetter<TModel, out TProperty>(in TModel model) where TModel : allows ref struct;
#else
    public delegate void RefAction<TModel, in TProperty>(ref TModel model, TProperty value);
    public delegate TProperty RefGetter<TModel, out TProperty>(in TModel model);
#endif
}