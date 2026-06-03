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

        public AsyncEnumerator GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new(_parser, _readerFactory(), _ownsReader, _formatProvider, _stringPool, _bufferSize, cancellationToken);
        }

        IAsyncEnumerator<TModel> IAsyncEnumerable<TModel>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return GetAsyncEnumerator(cancellationToken);
        }

        public sealed class AsyncEnumerator : IAsyncEnumerator<TModel>
        {
            private enum ReadResult { Record, NeedData, End }

            private readonly FixedWidthParser<TModel> _parser;
            private readonly bool _ownsReader;
            private readonly IFormatProvider? _formatProvider;
            private readonly StringPool? _stringPool;
            private readonly CancellationToken _cancellationToken;
            private TextReader? _reader;
            private char[] _buffer;
            private int _start;    // start of the data not yet consumed
            private int _end;      // end of the valid data in the buffer
            private int _scanFrom; // position from which we have not yet searched for '\n'
            private bool _eof;
            private int _lineNumber;
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
                _buffer = ArrayPool<char>.Shared.Rent(bufferSize);
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
                    if (result == ReadResult.Record) return true;
                    if (result == ReadResult.End) return false;

                    PrepareRefill();
                    int read = await reader
                        .ReadAsync(_buffer.AsMemory(_end, _buffer.Length - _end), _cancellationToken)
                        .ConfigureAwait(false);
                    if (read == 0) _eof = true;
                    else _end += read;
                }
            }

            private ReadResult TryReadFromBuffer()
            {
                while (true)
                {
                    if (_scanFrom < _end)
                    {
                        int rel = _buffer.AsSpan(_scanFrom, _end - _scanFrom).IndexOf('\n');
                        if (rel >= 0)
                        {
                            int nlIndex = _scanFrom + rel;
                            int contentEnd = nlIndex;
                            if (contentEnd > _start && _buffer[contentEnd - 1] == '\r') contentEnd--;
                            var line = _buffer.AsSpan(_start, contentEnd - _start);
                            _start = nlIndex + 1;
                            _scanFrom = _start;
                            _lineNumber++;
                            if (!line.IsEmpty)
                            {
                                Parse(line);
                                return ReadResult.Record;
                            }
                            continue; // skip empty lines
                        }
                        _scanFrom = _end;
                    }

                    if (_eof)
                    {
                        if (_start < _end)
                        {
                            int contentEnd = _end;
                            if (contentEnd > _start && _buffer[contentEnd - 1] == '\r') contentEnd--;
                            var line = _buffer.AsSpan(_start, contentEnd - _start);
                            _start = _end;
                            _scanFrom = _end;
                            _lineNumber++;
                            if (!line.IsEmpty)
                            {
                                Parse(line);
                                return ReadResult.Record;
                            }
                        }
                        return ReadResult.End;
                    }

                    return ReadResult.NeedData;
                }
            }

            private void Parse(ReadOnlySpan<char> line)
            {
                if (!_parser.TryParse(line, _formatProvider, _stringPool, out _current))
                {
                    throw new FormatException(
                        $"Line {_lineNumber} could not be parsed into {typeof(TModel).Name}: \"{line.ToString()}\".");
                }
            }

            private void PrepareRefill()
            {
                if (_start > 0)
                {
                    int len = _end - _start;
                    if (len > 0) Array.Copy(_buffer, _start, _buffer, 0, len);
                    _end = len;
                    _scanFrom -= _start;
                    _start = 0;
                }
                if (_end == _buffer.Length)
                {
                    var bigger = ArrayPool<char>.Shared.Rent(_buffer.Length * 2);
                    Array.Copy(_buffer, 0, bigger, 0, _end);
                    ArrayPool<char>.Shared.Return(_buffer);
                    _buffer = bigger;
                }
            }

            public ValueTask DisposeAsync()
            {
                if (_buffer is not null)
                {
                    ArrayPool<char>.Shared.Return(_buffer);
                    _buffer = null!;
                }
                if (_ownsReader) _reader?.Dispose();
                _reader = null;
                return ValueTask.CompletedTask;
            }
        }
    }
}
