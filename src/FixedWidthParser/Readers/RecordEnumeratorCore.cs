using System.Collections;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Shared synchronous enumerator logic for the record readers, specialized by a
    /// <see langword="struct"/> <typeparamref name="TParser"/> strategy so the parse call is
    /// devirtualized. Reads lines in blocks into an <see cref="System.Buffers.ArrayPool{T}"/>
    /// buffer and slices them as <see cref="ReadOnlySpan{T}"/> straight into the strategy — no
    /// string allocated per line. Held by value inside the public reader enumerators, which forward
    /// to it; being a <see langword="struct"/> keeps <c>foreach</c> allocation-free.
    /// </summary>
    internal struct RecordEnumeratorCore<TModel, TParser> : IEnumerator<TModel>
        where TParser : struct, ILineParser<TModel>
    {
        private readonly TParser _strategy;
        private readonly bool _ownsReader;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private TextReader? _reader;
        private LineBufferState<char, CharLineFormat> _lines;
        private TModel _current;

        internal RecordEnumeratorCore(
            TParser strategy,
            TextReader reader,
            bool ownsReader,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize)
        {
            _strategy = strategy;
            _reader = reader;
            _ownsReader = ownsReader;
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
            var reader = _reader ?? throw new ObjectDisposedException(nameof(RecordEnumeratorCore<TModel, TParser>));
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

                Refill(reader);
            }
        }

        private void Parse(ReadOnlySpan<char> line)
        {
            if (!_strategy.TryParse(line, _formatProvider, _stringPool, out _current))
            {
                throw new FormatException(
                    $"Line {_lines.LineNumber} could not be parsed into {typeof(TModel).Name}: \"{line}\".");
            }
        }

        private void Refill(TextReader reader)
        {
            _lines.Compact();
            _lines.GrowIfFull();
            int read = reader.Read(_lines.Buffer, _lines.End, _lines.Buffer.Length - _lines.End);
            _lines.Advance(read);
        }

        public void Dispose()
        {
            _lines.Return();
#pragma warning disable IDISP007 // Don't dispose injected
            if (_ownsReader) _reader?.Dispose();
#pragma warning restore IDISP007 // Don't dispose injected
            _reader = null;
        }

        public readonly void Reset() => throw new NotSupportedException("Reading is single-pass.");
    }
}
