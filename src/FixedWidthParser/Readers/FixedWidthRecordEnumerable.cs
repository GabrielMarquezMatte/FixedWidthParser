using System.Buffers;
using System.Collections;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Parsers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// A lazily-read sequence of models from a <see cref="TextReader"/>, <b>without allocating a
    /// string per line</b>: lines are read in blocks into a character buffer rented from the
    /// <see cref="ArrayPool{T}"/> and sliced as <see cref="ReadOnlySpan{T}"/> straight into the
    /// parser. Exposes a <see langword="struct"/> enumerator for allocation-free iteration in
    /// <c>foreach</c>, and implements <see cref="IEnumerable{T}"/> for LINQ interop.
    /// </summary>
    public sealed class FixedWidthRecordEnumerable<TModel> : IEnumerable<TModel> where TModel : new()
    {
        private readonly FixedWidthParser<TModel> _parser;
        private readonly Func<TextReader> _readerFactory;
        private readonly bool _ownsReader;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly int _bufferSize;

        internal FixedWidthRecordEnumerable(
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

        /// <summary>Struct enumerator: <c>foreach</c> iteration without heap allocation.</summary>
        public Enumerator GetEnumerator()
            => new(_parser, _readerFactory(), _ownsReader, _formatProvider, _stringPool, _bufferSize);

        IEnumerator<TModel> IEnumerable<TModel>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<TModel>
        {
            private readonly FixedWidthParser<TModel> _parser;
            private readonly bool _ownsReader;
            private readonly IFormatProvider? _formatProvider;
            private readonly StringPool? _stringPool;
            private TextReader? _reader;
            private LineBufferState _lines;
            private TModel _current;

            internal Enumerator(
                FixedWidthParser<TModel> parser,
                TextReader reader,
                bool ownsReader,
                IFormatProvider? formatProvider,
                StringPool? stringPool,
                int bufferSize)
            {
                _parser = parser;
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
                var reader = _reader ?? throw new ObjectDisposedException(nameof(Enumerator));
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
                if (!_parser.TryParse(line, _formatProvider, _stringPool, out _current))
                {
                    throw new FormatException(
                        $"Line {_lines.LineNumber} could not be parsed into {typeof(TModel).Name}: \"{line.ToString()}\".");
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
}
