using System.Runtime.ExceptionServices;
using FixedWidthParser.Processors;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Parsers
{
    public sealed class FixedWidthParser<TModel> where TModel : new(), allows ref struct
    {
        private static readonly Func<TModel> _modelFactory;
        private static readonly ColumnParserInfo<ColumnParser<TModel>>[] _processors;
        private static readonly int _requiredLineLength;
        private static readonly ExceptionDispatchInfo? _buildError;

        // The build is static (once per type). Any layout/configuration error is captured and
        // rethrown from the instance constructor, so the caller gets a clean exception instead of
        // a TypeInitializationException on first use.
        static FixedWidthParser()
        {
            try
            {
                _modelFactory = ParserBuilder.BuildModelFactory<TModel>();
                _processors = ParserBuilder.BuildProcessors<TModel, ColumnParser<TModel>>(ColumnParserFactory.Create<TModel>);
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
        public FixedWidthParser()
        {
            _buildError?.Throw();
        }
        public bool TryParse(ReadOnlySpan<char> line, IFormatProvider? formatProvider, StringPool? stringPool, out TModel model)
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
