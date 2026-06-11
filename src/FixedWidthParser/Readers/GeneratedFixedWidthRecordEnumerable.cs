using System.Collections;
using System.Diagnostics.CodeAnalysis;
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
        private readonly TextReaderSource _source;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly int _bufferSize;

        internal GeneratedFixedWidthRecordEnumerable(
            TextReaderSource source,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize)
        {
            _source = source;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _bufferSize = bufferSize;
        }

        /// <summary>Struct enumerator: <c>foreach</c> iteration without heap allocation.</summary>
        public Enumerator GetEnumerator()
        {
            return new(_source.Create(_bufferSize), _source.OwnsReader, _formatProvider, _stringPool, _bufferSize);
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
            {
                _core = new(default, reader, ownsReader, formatProvider, stringPool, bufferSize);
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
