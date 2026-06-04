using CommunityToolkit.HighPerformance.Buffers;
using csFastFloat;

namespace FixedWidthParser.Processors
{
    public sealed class FloatColumnProcessor<TModel>(int start, int length, RefAction<TModel, float> setter) : IColumnProcessor<TModel> where TModel : allows ref struct
    {
        public bool TryProcess(ref TModel model, IFormatProvider? formatProvider, ReadOnlySpan<char> value, StringPool? stringPool)
        {
            if (start >= value.Length)
            {
                return false;
            }
            var sliceLength = Math.Min(length, value.Length - start);
            var slice = value.Slice(start, sliceLength);
            var decimalSeparator = CultureHelpers.GetDecimalSeparator(formatProvider);
            if (!FastFloatParser.TryParseFloat(slice, out var parsedValue, decimal_separator: decimalSeparator))
            {
                return false;
            }
            setter(ref model, parsedValue);
            return true;
        }
    }
}