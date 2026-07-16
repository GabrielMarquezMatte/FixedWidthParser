using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Asynchronous source-generated read path: scans buffered lines and parses them through
    /// <see cref="IFixedWidthModel{TSelf}.TryParse"/> without reflection or delegates. The enumerator
    /// logic lives in the shared <see cref="AsyncRecordEnumeratorCore{T, TFormat, TModel, TParser, TSource}"/>, specialized
    /// here with the source-generated <see cref="GeneratedLineParser{TModel}"/> strategy.
    /// </summary>
    public sealed class GeneratedFixedWidthAsyncRecordEnumerable<TModel> : IAsyncEnumerable<TModel>
        where TModel : IFixedWidthModel<TModel>
    {
        private readonly TextReaderSource _source;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly int _bufferSize;

        internal GeneratedFixedWidthAsyncRecordEnumerable(
            TextReaderSource source,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize)
        {
            _source = source;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _bufferSize = bufferSize;
        }

#pragma warning disable HLQ006 // GetEnumerator() or GetAsyncEnumerator() should return a value type
        public AsyncRecordEnumeratorCore<char, CharLineFormat, TModel, GeneratedLineParser<TModel>, TextReaderSource> GetAsyncEnumerator(CancellationToken cancellationToken = default)
#pragma warning restore HLQ006 // GetEnumerator() or GetAsyncEnumerator() should return a value type
        {
            return new(default, _source.Create(_bufferSize), _formatProvider, _stringPool, _bufferSize, cancellationToken);
        }

        IAsyncEnumerator<TModel> IAsyncEnumerable<TModel>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return GetAsyncEnumerator(cancellationToken);
        }
    }
}
