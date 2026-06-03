using CommunityToolkit.HighPerformance.Buffers;
using csFastFloat;

namespace FixedWidthParser.Processors
{
    public sealed class DoubleColumnProcessor<TModel> : IColumnProcessor<TModel> where TModel : allows ref struct
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
            if (_start >= value.Length)
            {
                return false;
            }
            var length = Math.Min(_length, value.Length - _start);
            var slice = value.Slice(_start, length);
            var decimalSeparator = CultureHelpers.GetDecimalSeparator(formatProvider);
            if (!FastDoubleParser.TryParseDouble(slice, out var parsedValue, decimal_separator: decimalSeparator))
            {
                return false;
            }
            _setter(ref model, parsedValue);
            return true;
        }
    }
}