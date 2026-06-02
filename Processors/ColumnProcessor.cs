using CommunityToolkit.HighPerformance.Buffers;

namespace Benchmarks.Processors
{
    public sealed class ColumnProcessor<TModel, TProperty> : IColumnProcessor<TModel> where TProperty : ISpanParsable<TProperty>
    {
        private readonly int _start;
        private readonly int _length;
        private readonly RefAction<TModel, TProperty> _setter;
        public ColumnProcessor(int start, int length, RefAction<TModel, TProperty> setter)
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
            var slice = value.Slice(_start, length).TrimEnd(' ');
            if (!TProperty.TryParse(slice, formatProvider, out var parsedValue))
            {
                return false;
            }
            _setter(ref model, parsedValue);
            return true;
        }
    }
}