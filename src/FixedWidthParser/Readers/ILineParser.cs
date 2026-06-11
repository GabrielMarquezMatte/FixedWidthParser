using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Parsers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Strategy that parses a single fixed-width line into a <typeparamref name="TModel"/>.
    /// Implemented by <see langword="struct"/> types so the shared enumerator cores can be
    /// specialized per strategy: a <c>struct</c> type argument lets the JIT monomorphize each
    /// <c>(TModel, TParser)</c> pair and devirtualize <see cref="TryParse"/> into a direct call —
    /// no delegate, no boxing, no virtual dispatch on the hot path.
    /// </summary>
    public interface ILineParser<TModel>
    {
        /// <inheritdoc cref="IFixedWidthModel{TSelf}.TryParse"/>
        bool TryParse(ReadOnlySpan<char> line, IFormatProvider? formatProvider, StringPool? stringPool, out TModel model);
    }

    // Strategy structs are plumbing for the generic enumerator cores; they are never compared, so
    // value equality is meaningless for them.
#pragma warning disable CA1815 // Override equals and operator equals on value types
    /// <summary>
    /// Reflection-based parse strategy: forwards to a runtime <see cref="FixedWidthParser{TModel}"/>.
    /// </summary>
    public readonly struct ReflectionLineParser<TModel>(FixedWidthParser<TModel> parser) : ILineParser<TModel> where TModel : new()
    {
        public bool TryParse(ReadOnlySpan<char> line, IFormatProvider? formatProvider, StringPool? stringPool, out TModel model)
        {
            return parser.TryParse(line, formatProvider, stringPool, out model);
        }
    }

    /// <summary>
    /// Source-generated parse strategy: forwards to the model's static
    /// <see cref="IFixedWidthModel{TSelf}.TryParse"/>, avoiding reflection and delegates.
    /// </summary>
    public readonly struct GeneratedLineParser<TModel> : ILineParser<TModel> where TModel : IFixedWidthModel<TModel>
    {
        public bool TryParse(ReadOnlySpan<char> line, IFormatProvider? formatProvider, StringPool? stringPool, out TModel model)
        {
            return TModel.TryParse(line, formatProvider, stringPool, out model);
        }
    }
#pragma warning restore CA1815 // Override equals and operator equals on value types
}
