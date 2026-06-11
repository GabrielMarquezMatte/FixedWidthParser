using System.Collections;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// UTF-8 / byte counterpart of <see cref="GeneratedFixedWidthRecordEnumerable{TModel}"/>: a
    /// lazily-read sequence that parses each record through the model's source-generated
    /// <see cref="IUtf8FixedWidthModel{TSelf}.TryParse"/> method, straight from raw bytes (no
    /// <see cref="StreamReader"/>, no transcode, no string per line), avoiding reflection and delegates.
    /// <para>
    /// The source is stored as either a fixed <see cref="Stream"/> (single-pass) or a file path
    /// (reopened per enumeration) — never a captured delegate, so a <c>Read</c> call allocates only the
    /// enumerable itself, no closure.
    /// </para>
    /// </summary>
    public sealed class GeneratedUtf8FixedWidthRecordEnumerable<TModel> : IEnumerable<TModel>
        where TModel : IUtf8FixedWidthModel<TModel>
    {
        private readonly Stream? _stream;
        private readonly string? _path;
        private readonly bool _ownsStream;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly int _bufferSize;

        /// <summary>Single-pass source: a fixed stream.</summary>
        internal GeneratedUtf8FixedWidthRecordEnumerable(
            Stream stream,
            bool ownsStream,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize)
        {
            _stream = stream;
            _ownsStream = ownsStream;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _bufferSize = bufferSize;
        }

        /// <summary>Re-enumerable source: a file path reopened on each enumeration.</summary>
        internal GeneratedUtf8FixedWidthRecordEnumerable(
            string path,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize)
        {
            _path = path;
            _ownsStream = true;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _bufferSize = bufferSize;
        }

        /// <summary>Struct enumerator: <c>foreach</c> iteration without heap allocation.</summary>
        public Enumerator GetEnumerator()
        {
            var stream = _stream ?? new FileStream(_path!, FileMode.Open, FileAccess.Read, FileShare.Read, _bufferSize);
            return new(stream, _ownsStream, _formatProvider, _stringPool, _bufferSize);
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


        /// <summary>
        /// Allocation-free <see langword="struct"/> enumerator. Forwards to the shared
        /// <see cref="Utf8RecordEnumeratorCore{TModel, TParser}"/>, specialized with the source-generated
        /// <see cref="GeneratedUtf8LineParser{TModel}"/> strategy (devirtualized parse, no extra heap).
        /// </summary>
        public struct Enumerator : IEnumerator<TModel>
        {
            private Utf8RecordEnumeratorCore<TModel, GeneratedUtf8LineParser<TModel>> _core;

            internal Enumerator(
                Stream stream,
                bool ownsStream,
                IFormatProvider? formatProvider,
                StringPool? stringPool,
                int bufferSize)
            {
                _core = new(default, stream, ownsStream, formatProvider, stringPool, bufferSize);
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
