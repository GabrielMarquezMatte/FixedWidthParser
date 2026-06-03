using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Parsers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Lê modelos a partir de fontes textuais (TextReader, Stream ou arquivo) de forma
    /// preguiçosa e com baixa alocação. Configurado uma vez com cultura, <see cref="StringPool"/>
    /// opcional e tamanho de buffer; reutilizável para várias leituras.
    /// </summary>
    public sealed class FixedWidthReader<TModel> where TModel : new()
    {
        private readonly FixedWidthParser<TModel> _parser = new();
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly int _bufferSize;

        public FixedWidthReader(IFormatProvider? formatProvider = null, StringPool? stringPool = null, int bufferSize = 4096)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 1);
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _bufferSize = bufferSize;
        }

        /// <summary>Lê de um <see cref="TextReader"/> existente (passagem única; não o descarta).</summary>
        public FixedWidthRecordEnumerable<TModel> Read(TextReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            return new FixedWidthRecordEnumerable<TModel>(
                _parser, () => reader, ownsReader: false, _formatProvider, _stringPool, _bufferSize);
        }

        /// <summary>
        /// Lê de uma <see cref="Stream"/> (passagem única). Descarta o <see cref="StreamReader"/>
        /// criado internamente; <paramref name="leaveOpen"/> controla o fechamento da Stream.
        /// </summary>
        public FixedWidthRecordEnumerable<TModel> Read(Stream stream, Encoding? encoding = null, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(stream);
            var enc = encoding ?? Encoding.UTF8;
            return new FixedWidthRecordEnumerable<TModel>(
                _parser,
                () => new StreamReader(stream, enc, detectEncodingFromByteOrderMarks: true, bufferSize: _bufferSize, leaveOpen: leaveOpen),
                ownsReader: true, _formatProvider, _stringPool, _bufferSize);
        }

        /// <summary>Lê de um arquivo. Reenumerável: cada iteração abre o arquivo novamente.</summary>
        public FixedWidthRecordEnumerable<TModel> ReadFile(string path, Encoding? encoding = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            var enc = encoding ?? Encoding.UTF8;
            return new FixedWidthRecordEnumerable<TModel>(
                _parser,
                () => new StreamReader(path, enc, detectEncodingFromByteOrderMarks: true),
                ownsReader: true, _formatProvider, _stringPool, _bufferSize);
        }

        /// <summary>Lê de um <see cref="TextReader"/> existente via await foreach (não o descarta).</summary>
        public FixedWidthAsyncRecordEnumerable<TModel> ReadAsync(TextReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            return new FixedWidthAsyncRecordEnumerable<TModel>(
                _parser, () => reader, ownsReader: false, _formatProvider, _stringPool, _bufferSize);
        }

        /// <summary>
        /// Lê de uma <see cref="Stream"/> via await foreach (passagem única). Descarta o
        /// <see cref="StreamReader"/> criado; <paramref name="leaveOpen"/> controla a Stream.
        /// </summary>
        public FixedWidthAsyncRecordEnumerable<TModel> ReadAsync(Stream stream, Encoding? encoding = null, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(stream);
            var enc = encoding ?? Encoding.UTF8;
            return new FixedWidthAsyncRecordEnumerable<TModel>(
                _parser,
                () => new StreamReader(stream, enc, detectEncodingFromByteOrderMarks: true, bufferSize: _bufferSize, leaveOpen: leaveOpen),
                ownsReader: true, _formatProvider, _stringPool, _bufferSize);
        }

        /// <summary>
        /// Lê de um arquivo via await foreach, com I/O assíncrono de verdade (FileStream
        /// useAsync). Reenumerável: cada iteração reabre o arquivo.
        /// </summary>
        public FixedWidthAsyncRecordEnumerable<TModel> ReadFileAsync(string path, Encoding? encoding = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            var enc = encoding ?? Encoding.UTF8;
            return new FixedWidthAsyncRecordEnumerable<TModel>(
                _parser,
                () => new StreamReader(
                    new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, _bufferSize, useAsync: true),
                    enc, detectEncodingFromByteOrderMarks: true, bufferSize: _bufferSize),
                ownsReader: true, _formatProvider, _stringPool, _bufferSize);
        }
    }
}
