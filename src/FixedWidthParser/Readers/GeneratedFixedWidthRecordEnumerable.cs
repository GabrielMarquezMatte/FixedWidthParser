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
        public RecordEnumeratorCore<char, CharLineFormat, TModel, GeneratedLineParser<TModel>, TextReaderSource> GetEnumerator()
        {
            return new(default, _source.Create(_bufferSize), _formatProvider, _stringPool, _bufferSize);
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
