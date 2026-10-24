using System.ComponentModel;
using FixedWidthParser.Readers;

namespace FixedWidthParser.Infrastructure
{
    /// <summary>
    /// Asynchronous record enumerator over a stream. The line scan stays synchronous; only the refill
    /// awaits the stream.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class AsyncRecordEnumerator<TModel, TParser> : IAsyncEnumerator<TModel>
        where TParser : struct, IRecordLineParser<TModel>
    {
        private readonly TParser _parser;
        private readonly FixedWidthContext _context;
        private readonly Func<FixedWidthParseException, bool>? _onParseError;
        private readonly Stream _stream;
        private readonly bool _ownsStream;
        private readonly CancellationToken _cancellationToken;
        private LineBufferState _lines;
        private TModel _current;
        private bool _disposed;

        internal AsyncRecordEnumerator(TParser parser, Stream stream, bool ownsStream, FixedWidthReaderOptions options, CancellationToken cancellationToken)
        {
            _parser = parser;
            _context = options.Context;
            _onParseError = options.OnParseError;
            _stream = stream;
            _ownsStream = ownsStream;
            _cancellationToken = cancellationToken;
            _lines = default;
            _lines.Rent(options);
            _current = default!;
        }

        /// <inheritdoc />
        public TModel Current => _current;

        /// <inheritdoc />
        public ValueTask<bool> MoveNextAsync()
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(AsyncRecordEnumerator<TModel, TParser>));
            _cancellationToken.ThrowIfCancellationRequested();
            var status = TryReadFromBuffer();
            if (status != LineStatus.NeedData)
            {
                return new ValueTask<bool>(status == LineStatus.Line);
            }

            return MoveNextSlowAsync();
        }

        private async ValueTask<bool> MoveNextSlowAsync()
        {
            while (true)
            {
                _lines.Compact();
                _lines.GrowIfFull();
                _lines.Advance(await _stream.ReadAsync(_lines.Buffer.AsMemory(_lines.End), _cancellationToken).ConfigureAwait(false));
                _cancellationToken.ThrowIfCancellationRequested();

                var status = TryReadFromBuffer();
                if (status != LineStatus.NeedData)
                {
                    return status == LineStatus.Line;
                }
            }
        }

        private LineStatus TryReadFromBuffer()
        {
            while (true)
            {
                var status = _lines.TryGetLine(out var line);
                if (status != LineStatus.Line || _parser.TryParse(line, in _context, out _current))
                {
                    return status;
                }
                ParseErrors.HandleFailure<TModel, TParser>(_parser, line, _lines.LineNumber, in _context, _onParseError);
            }
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _lines.Return();
            _disposed = true;
#pragma warning disable IDISP007 // Disposed only when the enumerable was told it owns the stream.
            return _ownsStream ? _stream.DisposeAsync() : ValueTask.CompletedTask;
#pragma warning restore IDISP007
        }
    }
}
