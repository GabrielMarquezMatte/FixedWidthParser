using System.IO.Pipelines;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Records read from a <see cref="PipeReader"/> via <c>await foreach</c>, letting the pipe own
    /// buffering and read-ahead. Single-pass: a pipe is consumed as it is read.
    /// </summary>
    public sealed class FixedWidthPipeRecordEnumerable<TModel, TParser> : IAsyncEnumerable<TModel>
        where TParser : struct, IRecordLineParser<TModel>
    {
        private readonly TParser _parser;
        private readonly PipeReader _reader;
        private readonly bool _completeReader;
        private readonly FixedWidthReaderOptions _options;

        internal FixedWidthPipeRecordEnumerable(TParser parser, PipeReader reader, bool completeReader, FixedWidthReaderOptions options)
        {
            _parser = parser;
            _reader = reader;
            _completeReader = completeReader;
            _options = options;
        }

#pragma warning disable HLQ006 // GetEnumerator() or GetAsyncEnumerator() should return a value type
        public PipeRecordEnumerator<TModel, TParser> GetAsyncEnumerator(CancellationToken cancellationToken = default)
#pragma warning restore HLQ006
        {
            return new(_parser, _reader, _completeReader, _options, cancellationToken);
        }

        IAsyncEnumerator<TModel> IAsyncEnumerable<TModel>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return GetAsyncEnumerator(cancellationToken);
        }
    }
}
