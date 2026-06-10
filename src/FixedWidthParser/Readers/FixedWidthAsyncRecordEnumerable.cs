using System.Buffers;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Parsers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Asynchronous version of <see cref="FixedWidthRecordEnumerable{TModel}"/>: reads from a
    /// <see cref="TextReader"/> via <c>await foreach</c>, also <b>without allocating a string per
    /// line</b> (<see cref="ArrayPool{T}"/> buffer + <see cref="ReadOnlySpan{T}"/> slices). The
    /// enumerator logic lives in the shared <see cref="AsyncRecordEnumeratorCore{TModel, TParser}"/>,
    /// specialized here with the reflection-based <see cref="ReflectionLineParser{TModel}"/> strategy.
    /// </summary>
    public sealed class FixedWidthAsyncRecordEnumerable<TModel> : IAsyncEnumerable<TModel> where TModel : new()
    {
        private readonly FixedWidthParser<TModel> _parser;
        private readonly TextReaderSource _source;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly int _bufferSize;

        internal FixedWidthAsyncRecordEnumerable(
            FixedWidthParser<TModel> parser,
            TextReaderSource source,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize)
        {
            _parser = parser;
            _source = source;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _bufferSize = bufferSize;
        }

#pragma warning disable HLQ006 // GetEnumerator() or GetAsyncEnumerator() should return a value type
        public AsyncRecordEnumeratorCore<TModel, ReflectionLineParser<TModel>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
#pragma warning restore HLQ006 // GetEnumerator() or GetAsyncEnumerator() should return a value type
        {
            return new(new ReflectionLineParser<TModel>(_parser), _source.Create(_bufferSize), _source.OwnsReader, _formatProvider, _stringPool, _bufferSize, cancellationToken);
        }

        IAsyncEnumerator<TModel> IAsyncEnumerable<TModel>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return GetAsyncEnumerator(cancellationToken);
        }
    }
}
