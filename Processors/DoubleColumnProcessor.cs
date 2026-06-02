using CommunityToolkit.HighPerformance.Buffers;
using csFastFloat;

namespace Benchmarks.Processors
{
    public sealed class DoubleColumnProcessor<TModel> : IColumnProcessor<TModel>
    {
        private readonly int _start;
        private readonly int _length;
        private readonly RefAction<TModel, double> _setter;
        public DoubleColumnProcessor(int start, int length, RefAction<TModel, double> setter)
        {
            _start = start;
            _length = length;
            _setter = setter;
        }
        public bool TryProcess(ref TModel model, IFormatProvider? formatProvider, ReadOnlySpan<char> value, StringPool? stringPool)
        {
            if (value.Length < _start + _length)
            {
                return false;
            }
            var slice = value.Slice(_start, _length).TrimEnd(' ');
            if (!FastDoubleParser.TryParseDouble(slice, out var parsedValue))
            {
                return false;
            }
            _setter(ref model, parsedValue);
            return true;
        }
    }
}