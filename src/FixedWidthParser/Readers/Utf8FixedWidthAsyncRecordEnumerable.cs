using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Parsers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Asynchronous version of <see cref="Utf8FixedWidthRecordEnumerable{TModel}"/>: reads from a
    /// <see cref="Stream"/> via <c>await foreach</c>, also straight from raw bytes (no
    /// <see cref="StreamReader"/>, no transcode, no string per line). The enumerator logic lives in
    /// <see cref="AsyncRecordEnumeratorCore{T, TFormat, TModel, TParser, TSource}"/>.
    /// <para>
    /// The source is stored as either a fixed <see cref="Stream"/> (single-pass) or a file path
    /// (reopened per enumeration with <c>useAsync</c>) — never a captured delegate, so a
    /// <c>ReadAsync</c> call allocates only the enumerable itself, no closure.
    /// </para>
    /// </summary>
    public sealed class Utf8FixedWidthAsyncRecordEnumerable<TModel> : IAsyncEnumerable<TModel> where TModel : new()
    {
        private readonly Utf8FixedWidthParser<TModel> _parser;
        private readonly Stream? _stream;
        private readonly string? _path;
        private readonly bool _ownsStream;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly int _bufferSize;

        /// <summary>Single-pass source: a fixed stream.</summary>
        internal Utf8FixedWidthAsyncRecordEnumerable(
            Utf8FixedWidthParser<TModel> parser,
            Stream stream,
            bool ownsStream,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize)
        {
            _parser = parser;
            _stream = stream;
            _ownsStream = ownsStream;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _bufferSize = bufferSize;
        }

        /// <summary>Re-enumerable source: a file path reopened (with useAsync) on each enumeration.</summary>
        internal Utf8FixedWidthAsyncRecordEnumerable(
            Utf8FixedWidthParser<TModel> parser,
            string path,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize)
        {
            _parser = parser;
            _path = path;
            _ownsStream = true;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _bufferSize = bufferSize;
        }

#pragma warning disable HLQ006 // GetEnumerator() or GetAsyncEnumerator() should return a value type
        public AsyncRecordEnumeratorCore<byte, Utf8LineFormat, TModel, ReflectionUtf8LineParser<TModel>, StreamSource> GetAsyncEnumerator(CancellationToken cancellationToken = default)
#pragma warning restore HLQ006 // GetEnumerator() or GetAsyncEnumerator() should return a value type
        {
            var stream = _stream ?? new FileStream(_path!, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return new(new ReflectionUtf8LineParser<TModel>(_parser), new StreamSource(stream, _ownsStream), _formatProvider, _stringPool, _bufferSize, cancellationToken);
        }

        IAsyncEnumerator<TModel> IAsyncEnumerable<TModel>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return GetAsyncEnumerator(cancellationToken);
        }
    }
}
