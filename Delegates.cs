namespace Benchmarks
{
    public delegate void RefAction<TModel, TProperty>(ref TModel model, TProperty value);
    public delegate TProperty RefGetter<TModel, TProperty>(in TModel model);
}