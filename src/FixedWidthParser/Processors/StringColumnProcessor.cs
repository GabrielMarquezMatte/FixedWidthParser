using CommunityToolkit.HighPerformance.Buffers;
namespace FixedWidthParser.Processors
{
    public sealed class StringColumnProcessor<TModel>(int start, int length, RefAction<TModel, string> setter) : IColumnProcessor<TModel> where TModel : allows ref struct
    {
        public bool TryProcess(ref TModel model, IFormatProvider? formatProvider, ReadOnlySpan<char> value, StringPool? pool)
        {
            if (start >= value.Length)
            {
                setter(ref model, string.Empty);
                return true;
            }
            var sliceLength = Math.Min(length, value.Length - start);
            var slice = value.Slice(start, sliceLength).TrimEnd(' ');
            if (pool is null)
            {
                setter(ref model, slice.ToString());
                return true;
            }
            var pooledString = pool.GetOrAdd(slice);
            setter(ref model, pooledString);
            return true;
        }
    }
}