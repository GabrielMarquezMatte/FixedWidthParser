using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Processors
{
    public sealed class ColumnProcessor<TModel, TProperty>(int start, int length, RefAction<TModel, TProperty> setter) : IColumnProcessor<TModel> where TProperty : ISpanParsable<TProperty> where TModel : allows ref struct
    {
        public bool TryProcess(ref TModel model, IFormatProvider? formatProvider, ReadOnlySpan<char> value, StringPool? stringPool)
        {
            if (start >= value.Length)
            {
                return false;
            }
            var sliceLength = Math.Min(length, value.Length - start);
            var slice = value.Slice(start, sliceLength).TrimEnd(' ');
            if (!TProperty.TryParse(slice, formatProvider, out var parsedValue))
            {
                return false;
            }
            setter(ref model, parsedValue);
            return true;
        }
    }
}