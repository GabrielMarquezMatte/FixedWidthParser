using System.Collections;
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
    /// </summary>
    public sealed class Utf8FixedWidthRecordEnumerable<TModel> : IEnumerable<TModel> where TModel : new()
    {
        private readonly Utf8FixedWidthParser<TModel> _parser;
        private readonly Func<Stream> _streamFactory;
        private readonly bool _ownsStream;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly int _bufferSize;

        internal Utf8FixedWidthRecordEnumerable(
            Utf8FixedWidthParser<TModel> parser,
            Func<Stream> streamFactory,
            bool ownsStream,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize)
        {
            _parser = parser;
            _streamFactory = streamFactory;
            _ownsStream = ownsStream;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _bufferSize = bufferSize;
        }

        /// <summary>Struct enumerator: <c>foreach</c> iteration without heap allocation.</summary>
        public Enumerator GetEnumerator()
            => new(_parser, _streamFactory(), _ownsStream, _formatProvider, _stringPool, _bufferSize);

        IEnumerator<TModel> IEnumerable<TModel>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Allocation-free <see langword="struct"/> enumerator forwarding to the shared core.</summary>
        public struct Enumerator : IEnumerator<TModel>
        {
            private Utf8RecordEnumeratorCore<TModel> _core;

            internal Enumerator(
                Utf8FixedWidthParser<TModel> parser,
                Stream stream,
                bool ownsStream,
                IFormatProvider? formatProvider,
                StringPool? stringPool,
                int bufferSize)
                => _core = new(parser, stream, ownsStream, formatProvider, stringPool, bufferSize);

            public readonly TModel Current => _core.Current;
            readonly object IEnumerator.Current => _core.Current!;

            public bool MoveNext() => _core.MoveNext();
            public void Dispose() => _core.Dispose();
            public readonly void Reset() => _core.Reset();
        }
    }
}
