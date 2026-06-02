using CommunityToolkit.HighPerformance.Buffers;

namespace Benchmarks.Processors
{
    public interface IColumnProcessor<TModel> where TModel : allows ref struct
    {
        bool TryProcess(ref TModel model, IFormatProvider? formatProvider, ReadOnlySpan<char> value, StringPool? stringPool);
    }
}