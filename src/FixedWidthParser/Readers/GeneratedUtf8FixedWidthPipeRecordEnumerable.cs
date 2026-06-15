using System.IO.Pipelines;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// <see cref="PipeReader"/> source for the source-generated UTF-8 path: parses fixed-width records
    /// straight off a pipe via <c>await foreach</c> through <see cref="IUtf8FixedWidthModel{TSelf}.TryParse"/>
    /// (no reflection, no delegates). The enumerator logic lives in
    /// <see cref="Utf8PipeRecordEnumeratorCore{TModel, TParser}"/>, specialized with the
    /// <see cref="GeneratedUtf8LineParser{TModel}"/> strategy. Single-pass.
    /// </summary>
    public sealed class GeneratedUtf8FixedWidthPipeRecordEnumerable<TModel> : IAsyncEnumerable<TModel>
        where TModel : IUtf8FixedWidthModel<TModel>
    {
        private readonly PipeReader _reader;
        private readonly bool _completeReader;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;

        internal GeneratedUtf8FixedWidthPipeRecordEnumerable(
            PipeReader reader,
            bool completeReader,
            IFormatProvider? formatProvider,
            StringPool? stringPool)
        {
            _reader = reader;
            _completeReader = completeReader;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
        }

#pragma warning disable HLQ006 // GetEnumerator() or GetAsyncEnumerator() should return a value type
        public Utf8PipeRecordEnumeratorCore<TModel, GeneratedUtf8LineParser<TModel>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
#pragma warning restore HLQ006 // GetEnumerator() or GetAsyncEnumerator() should return a value type
        {
            return new(default, _reader, _completeReader, _formatProvider, _stringPool, cancellationToken);
        }

        IAsyncEnumerator<TModel> IAsyncEnumerable<TModel>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return GetAsyncEnumerator(cancellationToken);
        }
    }
}
