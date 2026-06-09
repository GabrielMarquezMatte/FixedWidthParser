using System.Buffers;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Shared asynchronous enumerator logic for the record readers, specialized by a
    /// <see langword="struct"/> <typeparamref name="TParser"/> strategy (devirtualized parse). The
    /// enumerator is a class (not a struct) because an <c>async</c> method captures <c>this</c> by
    /// value — a struct enumerator would lose its state between calls. The span scanning happens in
    /// a synchronous step; only the buffer refill is awaited, using <see cref="Memory{T}"/> (which
    /// can cross the <c>await</c>). The public reader enumerators derive from this to keep their
    /// concrete return type while sharing one implementation.
    /// </summary>
    public sealed class AsyncRecordEnumeratorCore<TModel, TParser> : IAsyncEnumerator<TModel> where TParser : struct, ILineParser<TModel>
    {
        private readonly TParser _strategy;
        private readonly bool _ownsReader;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly CancellationToken _cancellationToken;
        private TextReader? _reader;
        private LineBufferState _lines;
        private TModel _current;

        internal AsyncRecordEnumeratorCore(
            TParser strategy,
            TextReader reader,
            bool ownsReader,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize,
            CancellationToken cancellationToken)
        {
            _strategy = strategy;
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
            var reader = _reader ?? throw new ObjectDisposedException(nameof(AsyncRecordEnumeratorCore<TModel, TParser>));
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
            if (!_strategy.TryParse(line, _formatProvider, _stringPool, out _current))
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
