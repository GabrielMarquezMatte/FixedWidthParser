using System.Text;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// A closure-free description of where a char reader gets its <see cref="TextReader"/> from:
    /// an existing reader (single-pass, not owned), a <see cref="Stream"/> wrapped in a
    /// <see cref="StreamReader"/> (single-pass, owned), or a file path reopened per enumeration
    /// (owned). Stored <b>by value</b> on the record enumerables, so a <c>Read</c> call allocates no
    /// <c>Func&lt;TextReader&gt;</c> delegate or capture — only the enumerable itself.
    /// </summary>
    internal readonly struct TextReaderSource
    {
        private readonly TextReader? _reader;
        private readonly Stream? _stream;
        private readonly string? _path;
        private readonly Encoding? _encoding;
        private readonly bool _leaveOpen;
        private readonly bool _useAsync;

        private TextReaderSource(
            TextReader? reader, Stream? stream, string? path, Encoding? encoding, bool leaveOpen, bool useAsync, bool ownsReader)
        {
            _reader = reader;
            _stream = stream;
            _path = path;
            _encoding = encoding;
            _leaveOpen = leaveOpen;
            _useAsync = useAsync;
            OwnsReader = ownsReader;
        }

        /// <summary>Whether the enumerator should dispose the reader (true except for an injected reader).</summary>
        public bool OwnsReader { get; }

        /// <summary>Existing reader: single pass, not disposed by the enumerator.</summary>
        public static TextReaderSource FromReader(TextReader reader)
        {
            return new(reader, null, null, null, leaveOpen: false, useAsync: false, ownsReader: false);
        }

        /// <summary>Stream wrapped in a <see cref="StreamReader"/>: single pass; <paramref name="leaveOpen"/> controls closing the stream.</summary>
        public static TextReaderSource FromStream(Stream stream, Encoding encoding, bool leaveOpen)
        {
            return new(null, stream, null, encoding, leaveOpen, useAsync: false, ownsReader: true);
        }

        /// <summary>File path reopened per enumeration; <paramref name="useAsync"/> opts into asynchronous file I/O.</summary>
        public static TextReaderSource FromFile(string path, Encoding encoding, bool useAsync)
        {
            return new(null, null, path, encoding, leaveOpen: false, useAsync, ownsReader: true);
        }

        /// <summary>
        /// Materializes the <see cref="TextReader"/>. Returns the injected reader as-is, or allocates a
        /// <see cref="StreamReader"/> (and, for an async file, a <see cref="FileStream"/>) owned by the
        /// caller. BOM detection mirrors the original reader behavior.
        /// </summary>
        public TextReader Create(int bufferSize)
        {
            if (_reader is not null)
            {
                return _reader;
            }
            if (_stream is not null)
            {
                return new StreamReader(_stream, _encoding, detectEncodingFromByteOrderMarks: true, bufferSize: bufferSize, leaveOpen: _leaveOpen);
            }
            if (_useAsync)
            {
                return new StreamReader(
                    new FileStream(_path!, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true),
                    _encoding, detectEncodingFromByteOrderMarks: true, bufferSize: bufferSize);
            }
            return new StreamReader(_path!, _encoding, detectEncodingFromByteOrderMarks: true, bufferSize: bufferSize);
        }
    }
}
