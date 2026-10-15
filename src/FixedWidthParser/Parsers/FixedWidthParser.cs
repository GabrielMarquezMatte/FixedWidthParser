using System.Runtime.ExceptionServices;

namespace FixedWidthParser.Parsers
{
    /// <summary>
    /// Runtime (reflection-built) parser for one fixed-width line of bytes. Accepts <c>ref struct</c>
    /// models; for reading many lines use <see cref="Readers.FixedWidthReader{TModel}"/>.
    /// <para><b>Column positions are bytes.</b></para>
    /// </summary>
    public sealed class FixedWidthParser<TModel> where TModel : new(), allows ref struct
    {
        private static readonly Func<TModel> _modelFactory;
        private static readonly ColumnParserInfo<TModel>[] _processors;
        private static readonly int _requiredLineLength;
        private static readonly ExceptionDispatchInfo? _buildError;

        // The build is static (once per type). Any layout/configuration error is captured and
        // rethrown from the instance constructor, so the caller gets a clean exception instead of
        // a TypeInitializationException on first use.
#pragma warning disable CA1810, S3963 // The try/catch needs a static constructor.
        static FixedWidthParser()
        {
            try
            {
                _modelFactory = ParserBuilder.BuildModelFactory<TModel>();
                _processors = ParserBuilder.BuildProcessors<TModel>();
                _requiredLineLength = ParserBuilder.ComputeRequiredLineLength<TModel>(_processors);
                _buildError = null;
            }
#pragma warning disable CA1031 // Rethrown from the instance constructor.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _modelFactory = null!;
                _processors = [];
                _requiredLineLength = 0;
                _buildError = ExceptionDispatchInfo.Capture(ex);
            }
        }
#pragma warning restore CA1810, S3963

        /// <summary>Builds the parser, throwing <see cref="InvalidOperationException"/> when the model's layout is invalid.</summary>
        public FixedWidthParser()
        {
            _buildError?.Throw();
        }

        /// <summary>
        /// Parses a single line, without its line break, into <paramref name="model"/>. Returns
        /// <see langword="false"/> when the line is shorter than the layout or a non-string column fails
        /// to parse, after reporting why through <paramref name="context"/>.
        /// </summary>
        public bool TryParse(ReadOnlySpan<byte> line, in FixedWidthContext context, out TModel model)
        {
            if (line.Length < _requiredLineLength)
            {
                context.ReportShortLine(_requiredLineLength);
                model = default!;
                return false;
            }

            model = _modelFactory();
            foreach (ref readonly var processor in _processors.AsSpan())
            {
                if (!processor.Parse(line.Slice(processor.Start, processor.Length), in context, ref model))
                {
                    context.ReportFailedColumn(processor.Name, processor.Start, processor.Length);
                    return false;
                }
            }
            return true;
        }
    }
}
