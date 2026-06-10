using FixedWidthParser.Parsers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Asynchronous version of <see cref="Utf8FixedWidthRecordEnumerable{TModel}"/>: reads from a
    /// <see cref="Stream"/> via <c>await foreach</c>, also straight from raw bytes (no
    /// <see cref="StreamReader"/>, no transcode, no string per line). The enumerator logic lives in
    /// <see cref="Utf8AsyncRecordEnumeratorCore{TModel}"/>.
    /// </summary>
    public sealed class Utf8FixedWidthAsyncRecordEnumerable<TModel> : IAsyncEnumerable<TModel> where TModel : new()
    {
        private readonly Utf8FixedWidthParser<TModel> _parser;
        private readonly Func<Stream> _streamFactory;
        private readonly bool _ownsStream;
        private readonly IFormatProvider? _formatProvider;
        private readonly int _bufferSize;

        internal Utf8FixedWidthAsyncRecordEnumerable(
            Utf8FixedWidthParser<TModel> parser,
            Func<Stream> streamFactory,
            bool ownsStream,
            IFormatProvider? formatProvider,
            int bufferSize)
        {
            _parser = parser;
            _streamFactory = streamFactory;
            _ownsStream = ownsStream;
            _formatProvider = formatProvider;
            _bufferSize = bufferSize;
        }

#pragma warning disable HLQ006 // GetEnumerator() or GetAsyncEnumerator() should return a value type
        public Utf8AsyncRecordEnumeratorCore<TModel> GetAsyncEnumerator(CancellationToken cancellationToken = default)
#pragma warning restore HLQ006 // GetEnumerator() or GetAsyncEnumerator() should return a value type
        {
            return new(_parser, _streamFactory(), _ownsStream, _formatProvider, _bufferSize, cancellationToken);
        }

        IAsyncEnumerator<TModel> IAsyncEnumerable<TModel>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return GetAsyncEnumerator(cancellationToken);
        }
    }
}
