using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace FixedWidthParser.Infrastructure
{
    /// <summary>
    /// A lazily-read sequence of models from a stream or a file, without a string per line: bytes are
    /// read in blocks into a pooled buffer and each line is parsed in place. Exposes a
    /// <see langword="struct"/> enumerator for allocation-free <c>foreach</c>. A stream is read once; a
    /// file is reopened on each enumeration.
    /// </summary>
    public sealed class FixedWidthRecordEnumerable<TModel, TParser> : IEnumerable<TModel>
        where TParser : struct, IRecordLineParser<TModel>
    {
        private readonly TParser _parser;
        private readonly Stream? _stream;
        private readonly string? _path;
        private readonly bool _ownsStream;
        private readonly FixedWidthReaderOptions _options;

        internal FixedWidthRecordEnumerable(TParser parser, Stream stream, bool ownsStream, FixedWidthReaderOptions options)
        {
            _parser = parser;
            _stream = stream;
            _ownsStream = ownsStream;
            _options = options;
        }

        internal FixedWidthRecordEnumerable(TParser parser, string path, FixedWidthReaderOptions options)
        {
            _parser = parser;
            _path = path;
            _ownsStream = true;
            _options = options;
        }

        /// <summary>Struct enumerator: <c>foreach</c> iteration without heap allocation.</summary>
        public RecordEnumerator<TModel, TParser> GetEnumerator()
        {
#pragma warning disable CA2000, IDISP001 // The enumerator owns and disposes the file stream.
            var stream = _stream ?? new FileStream(_path!, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, FileOptions.SequentialScan);
#pragma warning restore CA2000, IDISP001
            return new(_parser, stream, _ownsStream, _options);
        }

        IEnumerator<TModel> IEnumerable<TModel>.GetEnumerator()
        {
            return GetEnumerator();
        }

        [ExcludeFromCodeCoverage]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
