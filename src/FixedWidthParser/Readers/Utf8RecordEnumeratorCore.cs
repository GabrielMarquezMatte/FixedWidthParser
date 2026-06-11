using System.Collections;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Parsers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// UTF-8 / byte counterpart of <see cref="RecordEnumeratorCore{TModel, TParser}"/>: reads raw
    /// bytes from a <see cref="Stream"/> in blocks into an <see cref="System.Buffers.ArrayPool{T}"/>
    /// buffer and slices each line as <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/> straight into
    /// <see cref="Utf8FixedWidthParser{TModel}"/> — no <see cref="StreamReader"/>, no transcode, no
    /// string per line. Held by value inside the public reader enumerator (a <see langword="struct"/>),
    /// which forwards to it so <c>foreach</c> stays allocation-free.
    /// </summary>
    internal struct Utf8RecordEnumeratorCore<TModel> : IEnumerator<TModel> where TModel : new()
    {
        private readonly Utf8FixedWidthParser<TModel> _parser;
        private readonly bool _ownsStream;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private Stream? _stream;
        private LineBufferState<byte, Utf8LineFormat> _lines;
        private TModel _current;

        internal Utf8RecordEnumeratorCore(
            Utf8FixedWidthParser<TModel> parser,
            Stream stream,
            bool ownsStream,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize)
        {
            _parser = parser;
            _stream = stream;
            _ownsStream = ownsStream;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _lines = default;
            _lines.Rent(bufferSize);
            _current = default!;
        }

        public readonly TModel Current => _current;
        readonly object IEnumerator.Current => _current!;

        public bool MoveNext()
        {
            var stream = _stream ?? throw new ObjectDisposedException(nameof(Utf8RecordEnumeratorCore<>));
            while (true)
            {
                var status = _lines.TryGetLine(out var line);
                if (status == LineStatus.Line)
                {
                    Parse(line);
                    return true;
                }
                if (status == LineStatus.End)
                {
                    return false;
                }

                Refill(stream);
            }
        }

        private void Parse(ReadOnlySpan<byte> line)
        {
            if (!_parser.TryParse(line, _formatProvider, _stringPool, out _current))
            {
                throw new FormatException(
                    $"Line {_lines.LineNumber} could not be parsed into {typeof(TModel).Name}: \"{Encoding.UTF8.GetString(line)}\".");
            }
        }

        private void Refill(Stream stream)
        {
            _lines.Compact();
            _lines.GrowIfFull();
            int read = stream.Read(_lines.Buffer, _lines.End, _lines.Buffer.Length - _lines.End);
            _lines.Advance(read);
        }

        public void Dispose()
        {
            _lines.Return();
#pragma warning disable IDISP007 // Don't dispose injected
            if (_ownsStream) _stream?.Dispose();
#pragma warning restore IDISP007 // Don't dispose injected
            _stream = null;
        }

        public readonly void Reset() => throw new NotSupportedException("Reading is single-pass.");
    }
}
