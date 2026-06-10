using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Parsers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// UTF-8 / byte counterpart of <see cref="FixedWidthReader{TModel}"/>. Parses fixed-width records
    /// straight from raw <see cref="byte"/> spans and streams, avoiding the UTF-8 → UTF-16 transcode
    /// that the <c>char</c> reader pays through a <see cref="StreamReader"/>. Configured once with an
    /// optional culture, an optional <see cref="StringPool"/> and a buffer size; reusable across reads.
    /// <para>
    /// <b>Column offsets are measured in bytes</b> (see <see cref="Utf8FixedWidthParser{TModel}"/>),
    /// which is exact for the single-byte/ASCII payloads typical of flat files. String columns are
    /// decoded with <see cref="System.Text.Encoding.UTF8"/>; when a <see cref="StringPool"/> is
    /// supplied, repeated values are interned (decoded once, same instance reused). A leading UTF-8
    /// byte-order mark on the stream is skipped.
    /// </para>
    /// </summary>
    public sealed class FixedWidthByteReader<TModel> where TModel : new()
    {
        private readonly Utf8FixedWidthParser<TModel> _parser = new();
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly int _bufferSize;

        public FixedWidthByteReader(IFormatProvider? formatProvider = null, StringPool? stringPool = null, int bufferSize = 4096)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 1);
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _bufferSize = bufferSize;
        }

        /// <summary>
        /// Parses a single UTF-8 fixed-width line (a slice of raw bytes, without the line terminator)
        /// into <paramref name="model"/> using the configured culture and string pool. Returns
        /// <see langword="false"/> when the line is too short for the layout or a non-string column
        /// fails to parse.
        /// </summary>
        public bool TryParse(ReadOnlySpan<byte> line, out TModel model)
            => _parser.TryParse(line, _formatProvider, _stringPool, out model);

        /// <summary>
        /// Reads from a <see cref="Stream"/> (single pass) as raw bytes. <paramref name="leaveOpen"/>
        /// controls whether the stream is disposed when iteration completes.
        /// </summary>
        public Utf8FixedWidthRecordEnumerable<TModel> Read(Stream stream, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return new Utf8FixedWidthRecordEnumerable<TModel>(
                _parser, stream, ownsStream: !leaveOpen, _formatProvider, _stringPool, _bufferSize);
        }

        /// <summary>Reads from a file as raw bytes. Re-enumerable: each iteration opens the file again.</summary>
        public Utf8FixedWidthRecordEnumerable<TModel> ReadFile(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return new Utf8FixedWidthRecordEnumerable<TModel>(
                _parser, path, _formatProvider, _stringPool, _bufferSize);
        }

        /// <summary>
        /// Reads from a <see cref="Stream"/> via <c>await foreach</c> (single pass) as raw bytes.
        /// <paramref name="leaveOpen"/> controls whether the stream is disposed when iteration completes.
        /// </summary>
        public Utf8FixedWidthAsyncRecordEnumerable<TModel> ReadAsync(Stream stream, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return new Utf8FixedWidthAsyncRecordEnumerable<TModel>(
                _parser, stream, ownsStream: !leaveOpen, _formatProvider, _stringPool, _bufferSize);
        }

        /// <summary>
        /// Reads from a file via <c>await foreach</c>, with true asynchronous I/O (FileStream useAsync).
        /// Re-enumerable: each iteration reopens the file.
        /// </summary>
        public Utf8FixedWidthAsyncRecordEnumerable<TModel> ReadFileAsync(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return new Utf8FixedWidthAsyncRecordEnumerable<TModel>(
                _parser, path, _formatProvider, _stringPool, _bufferSize);
        }
    }
}
