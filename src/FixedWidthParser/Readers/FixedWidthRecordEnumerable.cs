using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// A lazily-read sequence of models from a <see cref="TextReader"/>, <b>without allocating a
    /// string per line</b>: lines are read in blocks into a character buffer rented from the
    /// <see cref="ArrayPool{T}"/> and sliced as <see cref="ReadOnlySpan{T}"/> straight into the
    /// parser. Exposes a <see langword="struct"/> enumerator for allocation-free iteration in
    /// <c>foreach</c>, and implements <see cref="IEnumerable{T}"/> for LINQ interop.
    /// </summary>
    public sealed class FixedWidthRecordEnumerable<TModel, TParser> : IEnumerable<TModel>
        where TParser : struct, IRecordLineParser<char, TModel>
    {
        private readonly TParser _parser;
        private readonly TextReaderSource _source;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly int _bufferSize;

        internal FixedWidthRecordEnumerable(
            TParser parser,
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
        public RecordEnumeratorCore<char, CharLineFormat, TModel, TParser, TextReaderSource> GetEnumerator()
        {
            return new(_parser, _source.Create(_bufferSize), _formatProvider, _stringPool, _bufferSize);
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
