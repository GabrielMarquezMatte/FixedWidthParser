using System.Collections;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Parsers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// UTF-8 / byte counterpart of <see cref="FixedWidthRecordEnumerable{TModel}"/>: a lazily-read
    /// sequence of models read straight from a <see cref="Stream"/> as raw bytes (no
    /// <see cref="StreamReader"/>, no transcode, no string per line). Exposes a <see langword="struct"/>
    /// enumerator for allocation-free <c>foreach</c> and implements <see cref="IEnumerable{T}"/> for
    /// LINQ interop.
    /// <para>
    /// The source is stored as either a fixed <see cref="Stream"/> (single-pass) or a file path
    /// (reopened per enumeration) — never a captured delegate, so a <c>Read</c> call allocates only the
    /// enumerable itself, no closure.
    /// </para>
    /// </summary>
    public sealed class Utf8FixedWidthRecordEnumerable<TModel> : IEnumerable<TModel> where TModel : new()
    {
        private readonly Utf8FixedWidthParser<TModel> _parser;
        private readonly Stream? _stream;
        private readonly string? _path;
        private readonly bool _ownsStream;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly int _bufferSize;

        /// <summary>Single-pass source: a fixed stream.</summary>
        internal Utf8FixedWidthRecordEnumerable(
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
            _bufferSize = bufferSize;
        }

        /// <summary>Re-enumerable source: a file path reopened on each enumeration.</summary>
        internal Utf8FixedWidthRecordEnumerable(
            Utf8FixedWidthParser<TModel> parser,
            string path,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize)
        {
            _parser = parser;
            _path = path;
            _ownsStream = true;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _bufferSize = bufferSize;
        }

        /// <summary>Struct enumerator: <c>foreach</c> iteration without heap allocation.</summary>
        public Enumerator GetEnumerator()
        {
            var stream = _stream ?? new FileStream(_path!, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, FileOptions.SequentialScan);
            return new(_parser, stream, _ownsStream, _formatProvider, _stringPool, _bufferSize);
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

        /// <summary>Allocation-free <see langword="struct"/> enumerator forwarding to the shared core.</summary>
        public struct Enumerator : IEnumerator<TModel>
        {
            private Utf8RecordEnumeratorCore<TModel, ReflectionUtf8LineParser<TModel>> _core;

            internal Enumerator(
                Utf8FixedWidthParser<TModel> parser,
                Stream stream,
                bool ownsStream,
                IFormatProvider? formatProvider,
                StringPool? stringPool,
                int bufferSize)
            {
                _core = new(new ReflectionUtf8LineParser<TModel>(parser), stream, ownsStream, formatProvider, stringPool, bufferSize);
            }

            public readonly TModel Current => _core.Current;
            [ExcludeFromCodeCoverage]
            readonly object IEnumerator.Current => _core.Current!;

            public bool MoveNext()
            {
                return _core.MoveNext();
            }

            public void Dispose()
            {
                _core.Dispose();
            }

            [ExcludeFromCodeCoverage]
            public readonly void Reset()
            {
                _core.Reset();
            }
        }
    }
}
