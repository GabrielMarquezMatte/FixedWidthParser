using CommunityToolkit.HighPerformance.Buffers;
namespace Benchmarks.Processors
{
    public sealed class StringColumnProcessor<TModel> : IColumnProcessor<TModel>
    {
        private readonly int _start;
        private readonly int _length;
        private readonly RefAction<TModel, string> _setter;
        public StringColumnProcessor(int start, int length, RefAction<TModel, string> setter)
        {
            _start = start;
            _length = length;
            _setter = setter;
        }
        public bool TryProcess(ref TModel model, IFormatProvider? formatProvider, ReadOnlySpan<char> value, StringPool? pool)
        {
            if (value.Length < _start + _length) return false;

            var slice = value.Slice(_start, _length).TrimEnd(' ');
            if (pool is null)
            {
                _setter(ref model, slice.ToString());
                return true;
            }
            var pooledString = pool.GetOrAdd(slice);
            _setter(ref model, pooledString);
            return true;
        }
    }
}