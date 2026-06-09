using System.Collections;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// A lazily-read sequence that parses each record through the model's source-generated
    /// <see cref="IFixedWidthModel{TSelf}.TryParse"/> method, avoiding reflection and delegates.
    /// </summary>
    public sealed class GeneratedFixedWidthRecordEnumerable<TModel> : IEnumerable<TModel>
        where TModel : IFixedWidthModel<TModel>
    {
        private readonly Func<TextReader> _readerFactory;
        private readonly bool _ownsReader;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly int _bufferSize;

        internal GeneratedFixedWidthRecordEnumerable(
            Func<TextReader> readerFactory,
            bool ownsReader,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize)
        {
            _readerFactory = readerFactory;
            _ownsReader = ownsReader;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _bufferSize = bufferSize;
        }

        /// <summary>Struct enumerator: <c>foreach</c> iteration without heap allocation.</summary>
        public Enumerator GetEnumerator()
            => new(_readerFactory(), _ownsReader, _formatProvider, _stringPool, _bufferSize);

        IEnumerator<TModel> IEnumerable<TModel>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Allocation-free <see langword="struct"/> enumerator. Forwards to the shared
        /// <see cref="RecordEnumeratorCore{TModel, TParser}"/>, specialized with the source-generated
        /// <see cref="GeneratedLineParser{TModel}"/> strategy (devirtualized parse, no extra heap).
        /// </summary>
        public struct Enumerator : IEnumerator<TModel>
        {
            private RecordEnumeratorCore<TModel, GeneratedLineParser<TModel>> _core;

            internal Enumerator(
                TextReader reader,
                bool ownsReader,
                IFormatProvider? formatProvider,
                StringPool? stringPool,
                int bufferSize)
                => _core = new(default, reader, ownsReader, formatProvider, stringPool, bufferSize);

            public readonly TModel Current => _core.Current;
            readonly object IEnumerator.Current => _core.Current!;

            public bool MoveNext() => _core.MoveNext();
            public void Dispose() => _core.Dispose();
            public readonly void Reset() => _core.Reset();
        }
    }
}
