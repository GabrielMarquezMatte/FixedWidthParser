using System.Buffers;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Parsers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Asynchronous version of <see cref="FixedWidthRecordEnumerable{TModel}"/>: reads from a
    /// <see cref="TextReader"/> via <c>await foreach</c>, also <b>without allocating a string per
    /// line</b> (<see cref="ArrayPool{T}"/> buffer + <see cref="ReadOnlySpan{T}"/> slices).
    /// The enumerator is a class (not a struct) because an <c>async</c> method captures
    /// <c>this</c> by value — a struct enumerator would lose its state between calls. The span
    /// scanning happens in a synchronous step; only the buffer refill is awaited, using
    /// <see cref="Memory{T}"/> (which can cross the <c>await</c>).
    /// </summary>
    public sealed class FixedWidthAsyncRecordEnumerable<TModel> : IAsyncEnumerable<TModel> where TModel : new()
    {
        private readonly FixedWidthParser<TModel> _parser;
        private readonly Func<TextReader> _readerFactory;
        private readonly bool _ownsReader;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly int _bufferSize;

        internal FixedWidthAsyncRecordEnumerable(
            FixedWidthParser<TModel> parser,
            Func<TextReader> readerFactory,
            bool ownsReader,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize)
        {
            _parser = parser;
            _readerFactory = readerFactory;
            _ownsReader = ownsReader;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _bufferSize = bufferSize;
        }

#pragma warning disable HLQ006 // GetEnumerator() or GetAsyncEnumerator() should return a value type
        public AsyncEnumerator GetAsyncEnumerator(CancellationToken cancellationToken = default)
#pragma warning restore HLQ006 // GetEnumerator() or GetAsyncEnumerator() should return a value type
        {
            return new(_parser, _readerFactory(), _ownsReader, _formatProvider, _stringPool, _bufferSize, cancellationToken);
        }

        IAsyncEnumerator<TModel> IAsyncEnumerable<TModel>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return GetAsyncEnumerator(cancellationToken);
        }

#pragma warning disable CA1034 // Nested types should not be visible
        public sealed class AsyncEnumerator : IAsyncEnumerator<TModel>
#pragma warning restore CA1034 // Nested types should not be visible
        {
            private readonly FixedWidthParser<TModel> _parser;
            private readonly bool _ownsReader;
            private readonly IFormatProvider? _formatProvider;
            private readonly StringPool? _stringPool;
            private readonly CancellationToken _cancellationToken;
            private TextReader? _reader;
            private LineBufferState _lines;
            private TModel _current;

            internal AsyncEnumerator(
                FixedWidthParser<TModel> parser,
                TextReader reader,
                bool ownsReader,
                IFormatProvider? formatProvider,
                StringPool? stringPool,
                int bufferSize,
                CancellationToken cancellationToken)
            {
                _parser = parser;
                _reader = reader;
                _ownsReader = ownsReader;
                _formatProvider = formatProvider;
                _stringPool = stringPool;
                _cancellationToken = cancellationToken;
                _lines = default;
                _lines.Rent(bufferSize);
                _current = default!;
            }

            public TModel Current => _current;

            public async ValueTask<bool> MoveNextAsync()
            {
                var reader = _reader ?? throw new ObjectDisposedException(nameof(AsyncEnumerator));
                while (true)
                {
                    _cancellationToken.ThrowIfCancellationRequested();

                    // Synchronous step: spans are confined here, never alive across the await.
                    var result = TryReadFromBuffer();
                    if (result == LineStatus.Line) return true;
                    if (result == LineStatus.End) return false;

                    PrepareRefill();
                    int read = await reader
                        .ReadAsync(_lines.Buffer.AsMemory(_lines.End, _lines.Buffer.Length - _lines.End), _cancellationToken)
                        .ConfigureAwait(false);
                    _lines.Advance(read);
                }
            }

            private LineStatus TryReadFromBuffer()
            {
                var status = _lines.TryGetLine(out var line);
                if (status == LineStatus.Line)
                {
                    Parse(line);
                    return LineStatus.Line;
                }
                if (status == LineStatus.End)
                {
                    return LineStatus.End;
                }
                return LineStatus.NeedData;
            }

            private void Parse(ReadOnlySpan<char> line)
            {
                if (!_parser.TryParse(line, _formatProvider, _stringPool, out _current))
                {
                    throw new FormatException(
                        $"Line {_lines.LineNumber} could not be parsed into {typeof(TModel).Name}: \"{line.ToString()}\".");
                }
            }

            private void PrepareRefill()
            {
                _lines.Compact();
                _lines.GrowIfFull();
            }

            public ValueTask DisposeAsync()
            {
                _lines.Return();
#pragma warning disable IDISP007 // Don't dispose injected
                if (_ownsReader) _reader?.Dispose();
#pragma warning restore IDISP007 // Don't dispose injected
                _reader = null;
                return ValueTask.CompletedTask;
            }
        }
    }
}
