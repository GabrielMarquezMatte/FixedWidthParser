using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Parsers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Reads models from text sources (TextReader, Stream or file) lazily and with low
    /// allocation. Configured once with culture, an optional <see cref="StringPool"/> and a
    /// buffer size; reusable across multiple reads.
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

        /// <summary>Reads from an existing <see cref="TextReader"/> (single pass; does not dispose it).</summary>
        public FixedWidthRecordEnumerable<TModel> Read(TextReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            return new FixedWidthRecordEnumerable<TModel>(
                _parser, () => reader, ownsReader: false, _formatProvider, _stringPool, _bufferSize);
        }

        /// <summary>
        /// Reads from a <see cref="Stream"/> (single pass). Disposes the internally created
        /// <see cref="StreamReader"/>; <paramref name="leaveOpen"/> controls closing the stream.
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

        /// <summary>Reads from a file. Re-enumerable: each iteration opens the file again.</summary>
        public FixedWidthRecordEnumerable<TModel> ReadFile(string path, Encoding? encoding = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            var enc = encoding ?? Encoding.UTF8;
            return new FixedWidthRecordEnumerable<TModel>(
                _parser,
                () => new StreamReader(path, enc, detectEncodingFromByteOrderMarks: true),
                ownsReader: true, _formatProvider, _stringPool, _bufferSize);
        }

        /// <summary>Reads from an existing <see cref="TextReader"/> via await foreach (does not dispose it).</summary>
        public FixedWidthAsyncRecordEnumerable<TModel> ReadAsync(TextReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            return new FixedWidthAsyncRecordEnumerable<TModel>(
                _parser, () => reader, ownsReader: false, _formatProvider, _stringPool, _bufferSize);
        }

        /// <summary>
        /// Reads from a <see cref="Stream"/> via await foreach (single pass). Disposes the created
        /// <see cref="StreamReader"/>; <paramref name="leaveOpen"/> controls the stream.
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
        /// Reads from a file via await foreach, with true asynchronous I/O (FileStream useAsync).
        /// Re-enumerable: each iteration reopens the file.
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
