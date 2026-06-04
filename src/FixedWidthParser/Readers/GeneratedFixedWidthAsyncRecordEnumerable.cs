using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Asynchronous source-generated read path: scans buffered lines and parses them through
    /// <see cref="IFixedWidthModel{TSelf}.TryParse"/> without reflection or delegates.
    /// </summary>
    public sealed class GeneratedFixedWidthAsyncRecordEnumerable<TModel> : IAsyncEnumerable<TModel>
        where TModel : IFixedWidthModel<TModel>
    {
        private readonly Func<TextReader> _readerFactory;
        private readonly bool _ownsReader;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly int _bufferSize;

        internal GeneratedFixedWidthAsyncRecordEnumerable(
            Func<TextReader> readerFactory,
            bool ownsReader,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize)
        {
            _readerFactory = readerFactory;
            _ownsReader = ownsReader;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _bufferSize = bufferSize;
        }

        public AsyncEnumerator GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new(_readerFactory(), _ownsReader, _formatProvider, _stringPool, _bufferSize, cancellationToken);
        }

        IAsyncEnumerator<TModel> IAsyncEnumerable<TModel>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return GetAsyncEnumerator(cancellationToken);
        }

        public sealed class AsyncEnumerator : IAsyncEnumerator<TModel>
        {
            private readonly bool _ownsReader;
            private readonly IFormatProvider? _formatProvider;
            private readonly StringPool? _stringPool;
            private readonly CancellationToken _cancellationToken;
            private TextReader? _reader;
            private LineBufferState _lines;
            private TModel _current;

            internal AsyncEnumerator(
                TextReader reader,
                bool ownsReader,
                IFormatProvider? formatProvider,
                StringPool? stringPool,
                int bufferSize,
                CancellationToken cancellationToken)
            {
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

                    var status = TryReadFromBuffer();
                    if (status == LineStatus.Line) return true;
                    if (status == LineStatus.End) return false;

                    PrepareRefill();
                    int read = await reader
                        .ReadAsync(_lines.Buffer.AsMemory(_lines.End, _lines.Buffer.Length - _lines.End), _cancellationToken)
                        .ConfigureAwait(false);
                    _lines.Advance(read);
                }
            }

            private LineStatus TryReadFromBuffer()
            {
                while (true)
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
            }

            private void Parse(ReadOnlySpan<char> line)
            {
                if (!TModel.TryParse(line, _formatProvider, _stringPool, out _current))
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
                if (_ownsReader) _reader?.Dispose();
                _reader = null;
                return ValueTask.CompletedTask;
            }
        }
    }
}
