namespace Benchmarks.Formatters
{
    public interface IColumnFormatter<TModel>
    {
        void Format(in TModel model, Span<char> buffer, IFormatProvider? formatProvider);
    }
}