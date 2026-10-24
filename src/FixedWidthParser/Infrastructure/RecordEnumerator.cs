using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using FixedWidthParser.Readers;

namespace FixedWidthParser.Infrastructure
{
    /// <summary>
    /// Synchronous record enumerator over a stream. A <see langword="struct"/> specialized by
    /// <typeparamref name="TParser"/>, so <c>foreach</c> allocates nothing and parsing devirtualizes.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct RecordEnumerator<TModel, TParser> : IEnumerator<TModel>
        where TParser : struct, IRecordLineParser<TModel>
    {
        private readonly TParser _parser;
        private readonly FixedWidthContext _context;
        private readonly Func<FixedWidthParseException, bool>? _onParseError;
        private readonly Stream _stream;
        private readonly bool _ownsStream;
        private LineBufferState _lines;
        private TModel _current;
        private bool _disposed;

        internal RecordEnumerator(TParser parser, Stream stream, bool ownsStream, FixedWidthReaderOptions options)
        {
            _parser = parser;
            _context = options.Context;
            _onParseError = options.OnParseError;
            _stream = stream;
            _ownsStream = ownsStream;
            _lines = default;
            _lines.Rent(options);
            _current = default!;
            _disposed = false;
        }

        /// <inheritdoc />
        public readonly TModel Current => _current;

        [ExcludeFromCodeCoverage]
        readonly object IEnumerator.Current => _current!;

        /// <inheritdoc />
        public bool MoveNext()
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(RecordEnumerator<TModel, TParser>));
            while (true)
            {
                var status = _lines.TryGetLine(out var line);
                if (status == LineStatus.Line)
                {
                    if (_parser.TryParse(line, in _context, out _current))
                    {
                        return true;
                    }
                    ParseErrors.HandleFailure<TModel, TParser>(_parser, line, _lines.LineNumber, in _context, _onParseError);
                    continue;
                }
                if (status == LineStatus.End)
                {
                    return false;
                }

                _lines.Compact();
                _lines.GrowIfFull();
                _lines.Advance(_stream.Read(_lines.Buffer.AsSpan(_lines.End)));
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _lines.Return();
            if (_ownsStream)
            {
#pragma warning disable IDISP007 // Disposed only when the enumerable was told it owns the stream.
                _stream.Dispose();
#pragma warning restore IDISP007
            }
            _disposed = true;
        }

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public readonly void Reset()
        {
            throw new NotSupportedException("Reading is single-pass.");
        }
    }
}
