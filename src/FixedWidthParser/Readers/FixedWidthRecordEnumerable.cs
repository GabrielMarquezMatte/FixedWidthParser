using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
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
        private readonly TextReaderSource _source;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly int _bufferSize;

        internal FixedWidthRecordEnumerable(
            FixedWidthParser<TModel> parser,
            TextReaderSource source,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize)
        {
            _parser = parser;
            _source = source;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _bufferSize = bufferSize;
        }

        /// <summary>Struct enumerator: <c>foreach</c> iteration without heap allocation.</summary>
        public Enumerator GetEnumerator()
            => new(_parser, _source.Create(_bufferSize), _source.OwnsReader, _formatProvider, _stringPool, _bufferSize);

        IEnumerator<TModel> IEnumerable<TModel>.GetEnumerator() => GetEnumerator();
        [ExcludeFromCodeCoverage]
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Allocation-free <see langword="struct"/> enumerator. Forwards to the shared
        /// <see cref="RecordEnumeratorCore{TModel, TParser}"/>, specialized with the reflection-based
        /// <see cref="ReflectionLineParser{TModel}"/> strategy (devirtualized parse, no extra heap).
        /// </summary>
        public struct Enumerator : IEnumerator<TModel>
        {
            private RecordEnumeratorCore<TModel, ReflectionLineParser<TModel>> _core;

            internal Enumerator(
                FixedWidthParser<TModel> parser,
                TextReader reader,
                bool ownsReader,
                IFormatProvider? formatProvider,
                StringPool? stringPool,
                int bufferSize)
                => _core = new(new ReflectionLineParser<TModel>(parser), reader, ownsReader, formatProvider, stringPool, bufferSize);

            public readonly TModel Current => _core.Current;
            [ExcludeFromCodeCoverage]
            readonly object IEnumerator.Current => _core.Current!;

            public bool MoveNext() => _core.MoveNext();
            public void Dispose() => _core.Dispose();
            [ExcludeFromCodeCoverage]
            public readonly void Reset() => _core.Reset();
        }
    }
}
