using System.Runtime.ExceptionServices;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Processors;

namespace FixedWidthParser.Parsers
{
    /// <summary>
    /// UTF-8 counterpart of <see cref="FixedWidthParser{TModel}"/>: parses a fixed-width line directly
    /// from raw <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/> (UTF-8), skipping the
    /// UTF-8 → UTF-16 transcode that the <c>char</c> parser pays via a <see cref="StreamReader"/>.
    /// <para>
    /// <b>Column offsets are measured in bytes.</b> This is exact for the single-byte (ASCII) data
    /// that fixed-width/flat files almost always use; for content with multi-byte UTF-8 (e.g. accented
    /// characters) a byte offset is not the same as a character offset, so this parser is intended for
    /// single-byte payloads. Non-string columns parse via a <c>FixedColumnAttribute.Converter</c>, csFastFloat
    /// (<c>double</c>/<c>float</c>) or <see cref="IUtf8SpanParsable{TSelf}"/>; <c>string</c> columns are UTF-8 decoded and, when a
    /// <see cref="StringPool"/> is supplied, interned through it (otherwise a fresh string per value).
    /// </para>
    /// </summary>
#if NET9_0_OR_GREATER
    public sealed class Utf8FixedWidthParser<TModel> where TModel : new(), allows ref struct
#else
    public sealed class Utf8FixedWidthParser<TModel> where TModel : new()
#endif
    {
        private static readonly Func<TModel> _modelFactory;
        private static readonly ColumnParserInfo<Utf8ColumnParser<TModel>>[] _processors;
        private static readonly int _requiredLineLength;
        private static readonly ExceptionDispatchInfo? _buildError;

        // The build is static (once per type). Any layout/configuration error is captured and
        // rethrown from the instance constructor, so the caller gets a clean exception instead of
        // a TypeInitializationException on first use. Mirrors FixedWidthParser<TModel>.
        static Utf8FixedWidthParser()
        {
            try
            {
                _modelFactory = ParserBuilder.BuildModelFactory<TModel>();
                _processors = ParserBuilder.BuildProcessors<TModel, Utf8ColumnParser<TModel>>(Utf8ColumnParserFactory.Create<TModel>);
                _requiredLineLength = ParserBuilder.ComputeRequiredLineLength(_processors);
                _buildError = null;
            }
            catch (Exception ex)
            {
                _modelFactory = null!;
                _processors = [];
                _requiredLineLength = 0;
                _buildError = ExceptionDispatchInfo.Capture(ex);
            }
        }
        public Utf8FixedWidthParser()
        {
            _buildError?.Throw();
        }
        /// <summary>
        /// Parses a single UTF-8 fixed-width line into <paramref name="model"/>. Returns
        /// <see langword="false"/> when the line is shorter (in bytes) than the configured layout or
        /// when a non-string column fails to parse. When <paramref name="stringPool"/> is supplied,
        /// string columns are interned through it (decoded as UTF-8).
        /// </summary>
        public bool TryParse(ReadOnlySpan<byte> line, IFormatProvider? formatProvider, StringPool? stringPool, out TModel model)
        {
            if (line.Length < _requiredLineLength)
            {
                model = default!;
                return false;
            }

            model = _modelFactory();
            foreach (ref readonly var processor in _processors.AsSpan())
            {
                var column = line.Slice(processor.Start, processor.Length);
                if (!processor.Parse(column, formatProvider, stringPool, ref model))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
