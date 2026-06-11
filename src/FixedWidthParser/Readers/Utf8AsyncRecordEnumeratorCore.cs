using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Parsers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// UTF-8 / byte counterpart of <see cref="AsyncRecordEnumeratorCore{TModel, TParser}"/>: reads raw
    /// bytes from a <see cref="Stream"/> via <c>await foreach</c>. A class (not a struct) because an
    /// <c>async</c> method captures <c>this</c> by value, which would lose a struct enumerator's state.
    /// The span scanning happens in a synchronous step; only the buffer refill is awaited, using
    /// <see cref="Memory{T}"/> (which can cross the <c>await</c>).
    /// </summary>
    public sealed class Utf8AsyncRecordEnumeratorCore<TModel> : IAsyncEnumerator<TModel> where TModel : new()
    {
        private readonly Utf8FixedWidthParser<TModel> _parser;
        private readonly bool _ownsStream;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly CancellationToken _cancellationToken;
        private Stream? _stream;
        private LineBufferState<byte, Utf8LineFormat> _lines;
        private TModel _current;

        internal Utf8AsyncRecordEnumeratorCore(
            Utf8FixedWidthParser<TModel> parser,
            Stream stream,
            bool ownsStream,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize,
            CancellationToken cancellationToken)
        {
            _parser = parser;
            _stream = stream;
            _ownsStream = ownsStream;
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
            var stream = _stream ?? throw new ObjectDisposedException(nameof(Utf8AsyncRecordEnumeratorCore<TModel>));
            while (true)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                // Synchronous step: spans are confined here, never alive across the await.
                var result = TryReadFromBuffer();
                if (result == LineStatus.Line) return true;
                if (result == LineStatus.End) return false;

                PrepareRefill();
                int read = await stream
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

        private void Parse(ReadOnlySpan<byte> line)
        {
            if (!_parser.TryParse(line, _formatProvider, _stringPool, out _current))
            {
                throw new FormatException(
                    $"Line {_lines.LineNumber} could not be parsed into {typeof(TModel).Name}: \"{Encoding.UTF8.GetString(line)}\".");
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
            if (_ownsStream) _stream?.Dispose();
#pragma warning restore IDISP007 // Don't dispose injected
            _stream = null;
            return ValueTask.CompletedTask;
        }
    }
}
