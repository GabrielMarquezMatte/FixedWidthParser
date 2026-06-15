using System.IO.Pipelines;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Parsers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// <see cref="PipeReader"/> source for the reflection-based UTF-8 path: parses fixed-width records
    /// straight off a pipe via <c>await foreach</c>, letting the pipe own buffering/read-ahead. The
    /// enumerator logic lives in <see cref="Utf8PipeRecordEnumeratorCore{TModel, TParser}"/>, specialized
    /// with the <see cref="ReflectionUtf8LineParser{TModel}"/> strategy. Single-pass (a pipe is consumed
    /// as it is read).
    /// </summary>
    public sealed class Utf8FixedWidthPipeRecordEnumerable<TModel> : IAsyncEnumerable<TModel> where TModel : new()
    {
        private readonly Utf8FixedWidthParser<TModel> _parser;
        private readonly PipeReader _reader;
        private readonly bool _completeReader;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;

        internal Utf8FixedWidthPipeRecordEnumerable(
            Utf8FixedWidthParser<TModel> parser,
            PipeReader reader,
            bool completeReader,
            IFormatProvider? formatProvider,
            StringPool? stringPool)
        {
            _parser = parser;
            _reader = reader;
            _completeReader = completeReader;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
        }

#pragma warning disable HLQ006 // GetEnumerator() or GetAsyncEnumerator() should return a value type
        public Utf8PipeRecordEnumeratorCore<TModel, ReflectionUtf8LineParser<TModel>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
#pragma warning restore HLQ006 // GetEnumerator() or GetAsyncEnumerator() should return a value type
        {
            return new(new ReflectionUtf8LineParser<TModel>(_parser), _reader, _completeReader, _formatProvider, _stringPool, cancellationToken);
        }

        IAsyncEnumerator<TModel> IAsyncEnumerable<TModel>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return GetAsyncEnumerator(cancellationToken);
        }
    }
}
