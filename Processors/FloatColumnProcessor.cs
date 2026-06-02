using CommunityToolkit.HighPerformance.Buffers;
using csFastFloat;

namespace Benchmarks.Processors
{
    public sealed class FloatColumnProcessor<TModel> : IColumnProcessor<TModel>
    {
        private readonly int _start;
        private readonly int _length;
        private readonly RefAction<TModel, float> _setter;
        public FloatColumnProcessor(int start, int length, RefAction<TModel, float> setter)
        {
            _start = start;
            _length = length;
            _setter = setter;
        }
        public bool TryProcess(ref TModel model, IFormatProvider? formatProvider, ReadOnlySpan<char> value, StringPool? stringPool)
        {
            if (_start >= value.Length)
            {
                return false;
            }
            var length = Math.Min(_length, value.Length - _start);
            var slice = value.Slice(_start, length);
            if (!FastFloatParser.TryParseFloat(slice, out var parsedValue))
            {
                return false;
            }
            _setter(ref model, parsedValue);
            return true;
        }
    }
}