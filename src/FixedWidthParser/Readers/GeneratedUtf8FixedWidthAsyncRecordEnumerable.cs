using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Asynchronous version of <see cref="GeneratedUtf8FixedWidthRecordEnumerable{TModel}"/>: reads from
    /// a <see cref="Stream"/> via <c>await foreach</c>, straight from raw bytes (no
    /// <see cref="StreamReader"/>, no transcode, no string per line), parsing each record through
    /// <see cref="IUtf8FixedWidthModel{TSelf}.TryParse"/> without reflection or delegates. The enumerator
    /// logic lives in the shared <see cref="AsyncRecordEnumeratorCore{T, TFormat, TModel, TParser, TSource}"/>, specialized
    /// here with the source-generated <see cref="GeneratedUtf8LineParser{TModel}"/> strategy.
    /// <para>
    /// The source is stored as either a fixed <see cref="Stream"/> (single-pass) or a file path
    /// (reopened per enumeration with <c>useAsync</c>) — never a captured delegate, so a
    /// <c>ReadAsync</c> call allocates only the enumerable itself, no closure.
    /// </para>
    /// </summary>
    public sealed class GeneratedUtf8FixedWidthAsyncRecordEnumerable<TModel> : IAsyncEnumerable<TModel>
        where TModel : IUtf8FixedWidthModel<TModel>
    {
        private readonly Stream? _stream;
        private readonly string? _path;
        private readonly bool _ownsStream;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly int _bufferSize;

        /// <summary>Single-pass source: a fixed stream.</summary>
        internal GeneratedUtf8FixedWidthAsyncRecordEnumerable(
            Stream stream,
            bool ownsStream,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize)
        {
            _stream = stream;
            _ownsStream = ownsStream;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _bufferSize = bufferSize;
        }

        /// <summary>Re-enumerable source: a file path reopened (with useAsync) on each enumeration.</summary>
        internal GeneratedUtf8FixedWidthAsyncRecordEnumerable(
            string path,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize)
        {
            _path = path;
            _ownsStream = true;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _bufferSize = bufferSize;
        }

#pragma warning disable HLQ006 // GetEnumerator() or GetAsyncEnumerator() should return a value type
        public AsyncRecordEnumeratorCore<byte, Utf8LineFormat, TModel, GeneratedUtf8LineParser<TModel>, StreamSource> GetAsyncEnumerator(CancellationToken cancellationToken = default)
#pragma warning restore HLQ006 // GetEnumerator() or GetAsyncEnumerator() should return a value type
        {
            var stream = _stream ?? new FileStream(_path!, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return new(default, new StreamSource(stream, _ownsStream), _formatProvider, _stringPool, _bufferSize, cancellationToken);
        }

        IAsyncEnumerator<TModel> IAsyncEnumerable<TModel>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return GetAsyncEnumerator(cancellationToken);
        }
    }
}
