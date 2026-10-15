namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Asynchronous version of <see cref="FixedWidthRecordEnumerable{TModel, TParser}"/> for
    /// <c>await foreach</c>. A stream is read once; a file is reopened, with asynchronous I/O, on each
    /// enumeration.
    /// </summary>
    public sealed class FixedWidthAsyncRecordEnumerable<TModel, TParser> : IAsyncEnumerable<TModel>
        where TParser : struct, IRecordLineParser<TModel>
    {
        private readonly TParser _parser;
        private readonly Stream? _stream;
        private readonly string? _path;
        private readonly bool _ownsStream;
        private readonly FixedWidthReaderOptions _options;

        internal FixedWidthAsyncRecordEnumerable(TParser parser, Stream stream, bool ownsStream, FixedWidthReaderOptions options)
        {
            _parser = parser;
            _stream = stream;
            _ownsStream = ownsStream;
            _options = options;
        }

        internal FixedWidthAsyncRecordEnumerable(TParser parser, string path, FixedWidthReaderOptions options)
        {
            _parser = parser;
            _path = path;
            _ownsStream = true;
            _options = options;
        }

#pragma warning disable HLQ006 // GetEnumerator() or GetAsyncEnumerator() should return a value type
        public AsyncRecordEnumerator<TModel, TParser> GetAsyncEnumerator(CancellationToken cancellationToken = default)
#pragma warning restore HLQ006
        {
#pragma warning disable CA2000, IDISP001 // The enumerator owns and disposes the file stream.
            var stream = _stream ?? new FileStream(_path!, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
#pragma warning restore CA2000, IDISP001
            return new(_parser, stream, _ownsStream, _options, cancellationToken);
        }

        IAsyncEnumerator<TModel> IAsyncEnumerable<TModel>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return GetAsyncEnumerator(cancellationToken);
        }
    }
}
