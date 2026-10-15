using FixedWidthParser.Parsers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Parse strategy the record enumerators are specialized by. Implemented by
    /// <see langword="struct"/> types so the JIT monomorphizes each <c>(TModel, TParser)</c> pair and
    /// devirtualizes <see cref="TryParse"/> into a direct call. Public only because it appears in the
    /// enumerator types; the library is its only implementer.
    /// </summary>
    public interface IRecordLineParser<TModel>
    {
        /// <summary>Parses a buffered line into a model.</summary>
        bool TryParse(ReadOnlySpan<byte> line, in FixedWidthContext context, out TModel model);
    }

    // Strategy structs are plumbing for the generic enumerators; they are never compared, so
    // value equality is meaningless for them.
#pragma warning disable CA1815 // Override equals and operator equals on value types
    /// <summary>Reflection-based parse strategy: forwards to a runtime <see cref="FixedWidthParser{TModel}"/>.</summary>
    public readonly struct ReflectionLineParser<TModel> : IRecordLineParser<TModel> where TModel : new()
    {
        private readonly FixedWidthParser<TModel> _parser;

        internal ReflectionLineParser(FixedWidthParser<TModel> parser)
        {
            _parser = parser;
        }

        /// <inheritdoc />
        public bool TryParse(ReadOnlySpan<byte> line, in FixedWidthContext context, out TModel model)
        {
            return _parser.TryParse(line, in context, out model);
        }
    }

    /// <summary>Source-generated parse strategy: forwards to the model's static <see cref="IFixedWidthModel{TSelf}.TryParse"/>.</summary>
    public readonly struct GeneratedLineParser<TModel> : IRecordLineParser<TModel> where TModel : IFixedWidthModel<TModel>
    {
        /// <inheritdoc />
        public bool TryParse(ReadOnlySpan<byte> line, in FixedWidthContext context, out TModel model)
        {
            return TModel.TryParse(line, in context, out model);
        }
    }
#pragma warning restore CA1815
}
