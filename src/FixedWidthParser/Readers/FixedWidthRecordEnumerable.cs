using System.Buffers;
using System.Collections;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Parsers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Sequência de modelos lida sob demanda de um <see cref="TextReader"/>, <b>sem alocar uma
    /// string por linha</b>: as linhas são lidas em blocos para um buffer de caracteres alugado
    /// do <see cref="ArrayPool{T}"/> e fatiadas como <see cref="ReadOnlySpan{T}"/> direto ao
    /// parser. Expõe um enumerador <see langword="struct"/> para iteração sem alocação em
    /// <c>foreach</c>, e implementa <see cref="IEnumerable{T}"/> para interoperar com LINQ.
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

        /// <summary>Enumerador struct: iteração em <c>foreach</c> sem alocar no heap.</summary>
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
            private char[] _buffer;
            private int _start;    // início dos dados ainda não consumidos
            private int _end;      // fim dos dados válidos no buffer
            private int _scanFrom; // posição a partir da qual ainda não procuramos '\n'
            private bool _eof;
            private int _lineNumber;
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
                _buffer = ArrayPool<char>.Shared.Rent(bufferSize);
                _start = 0;
                _end = 0;
                _scanFrom = 0;
                _eof = false;
                _lineNumber = 0;
                _current = default!;
            }

            public readonly TModel Current => _current;
            readonly object IEnumerator.Current => _current!;

            public bool MoveNext()
            {
                var reader = _reader ?? throw new ObjectDisposedException(nameof(Enumerator));
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
                                return true;
                            }
                            continue; // pula linhas vazias
                        }
                        _scanFrom = _end; // nenhum '\n' no que já foi lido
                    }

                    if (_eof)
                    {
                        // Última linha sem quebra de linha final.
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
                                return true;
                            }
                        }
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
                        $"Linha {_lineNumber} não pôde ser convertida em {typeof(TModel).Name}: \"{line.ToString()}\".");
                }
            }

            private void Refill(TextReader reader)
            {
                // Compacta os dados não consumidos para o início do buffer.
                if (_start > 0)
                {
                    int len = _end - _start;
                    if (len > 0) Array.Copy(_buffer, _start, _buffer, 0, len);
                    _end = len;
                    _scanFrom -= _start;
                    _start = 0;
                }
                // Buffer cheio com uma única linha parcial: dobra a capacidade.
                if (_end == _buffer.Length)
                {
                    var bigger = ArrayPool<char>.Shared.Rent(_buffer.Length * 2);
                    Array.Copy(_buffer, 0, bigger, 0, _end);
                    ArrayPool<char>.Shared.Return(_buffer);
                    _buffer = bigger;
                }
                int read = reader.Read(_buffer, _end, _buffer.Length - _end);
                if (read == 0) _eof = true;
                else _end += read;
            }

            public void Dispose()
            {
                if (_buffer is not null)
                {
                    ArrayPool<char>.Shared.Return(_buffer);
                    _buffer = null!;
                }
                if (_ownsReader) _reader?.Dispose();
                _reader = null;
            }

            public readonly void Reset() => throw new NotSupportedException("A leitura é de passagem única.");
        }
    }
}
