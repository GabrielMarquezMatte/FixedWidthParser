using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Parsers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// UTF-8 / byte counterpart of <see cref="ILineParser{TModel}"/>: a strategy that parses a single
    /// fixed-width line of raw <see cref="byte"/>s into a <typeparamref name="TModel"/>. Implemented by
    /// <see langword="struct"/> types so the shared UTF-8 enumerator cores can be specialized per
    /// strategy — a <c>struct</c> type argument lets the JIT monomorphize each <c>(TModel, TParser)</c>
    /// pair and devirtualize <see cref="TryParse"/> into a direct call (no delegate, no boxing, no
    /// virtual dispatch on the hot path).
    /// </summary>
    public interface IUtf8LineParser<TModel>
    {
        /// <inheritdoc cref="IUtf8FixedWidthModel{TSelf}.TryParse"/>
        bool TryParse(ReadOnlySpan<byte> line, IFormatProvider? formatProvider, StringPool? stringPool, out TModel model);
    }

    // Strategy structs are plumbing for the generic enumerator cores; they are never compared, so
    // value equality is meaningless for them.
#pragma warning disable CA1815 // Override equals and operator equals on value types
    /// <summary>
    /// Reflection-based parse strategy: forwards to a runtime <see cref="Utf8FixedWidthParser{TModel}"/>.
    /// </summary>
    public readonly struct ReflectionUtf8LineParser<TModel>(Utf8FixedWidthParser<TModel> parser) : IUtf8LineParser<TModel> where TModel : new()
    {
        public bool TryParse(ReadOnlySpan<byte> line, IFormatProvider? formatProvider, StringPool? stringPool, out TModel model)
        {
            return parser.TryParse(line, formatProvider, stringPool, out model);
        }
    }

    /// <summary>
    /// Source-generated parse strategy: forwards to the model's static
    /// <see cref="IUtf8FixedWidthModel{TSelf}.TryParse"/>, avoiding reflection and delegates.
    /// </summary>
    public readonly struct GeneratedUtf8LineParser<TModel> : IUtf8LineParser<TModel> where TModel : IUtf8FixedWidthModel<TModel>
    {
        public bool TryParse(ReadOnlySpan<byte> line, IFormatProvider? formatProvider, StringPool? stringPool, out TModel model)
        {
            return TModel.TryParse(line, formatProvider, stringPool, out model);
        }

    }
#pragma warning restore CA1815 // Override equals and operator equals on value types
}
