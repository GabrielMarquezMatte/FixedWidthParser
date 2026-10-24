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
        where TModel : allows ref struct
        where TParser : struct, IRecordLineParser<TModel>
    {
        private readonly TParser _parser;
        private readonly FixedWidthContext _context;
        private readonly Func<FixedWidthParseException, bool>? _onParseError;
        private readonly Stream _stream;
        private readonly bool _ownsStream;
        private readonly CancellationToken _cancellationToken;
        private LineBufferState _lines;
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
        }

        /// <summary>The record on the line the enumerator sits on, parsed on access.</summary>
        /// <remarks>
        /// Parsed here rather than by <see cref="MoveNextAsync"/> because the enumerator cannot hold a field
        /// of a <c>ref struct</c> <typeparamref name="TModel"/>. <c>await foreach</c> reads it once per line,
        /// so each line is still parsed once; reading it twice parses twice.
        /// </remarks>
        /// <exception cref="FixedWidthParseException">The line does not parse.</exception>
        public TModel Current => ParseErrors.Parse<TModel, TParser>(_parser, _lines.CurrentLine, _lines.LineNumber, in _context);

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
                if (status != LineStatus.Line || ParseErrors.Accepts<TModel, TParser>(_parser, line, _lines.LineNumber, in _context, _onParseError))
                {
                    return status;
                }
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
