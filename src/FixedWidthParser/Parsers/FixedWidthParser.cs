using System.Diagnostics.CodeAnalysis;

namespace FixedWidthParser.Parsers
{
    /// <summary>
    /// Runtime (reflection-built) parser for one fixed-width line of bytes. Accepts <c>ref struct</c>
    /// models; for reading many lines use <see cref="Readers.FixedWidthReader{TModel}"/>.
    /// <para><b>Column positions are bytes.</b></para>
    /// </summary>
    public sealed class FixedWidthParser<TModel> where TModel : new(), allows ref struct
    {
        private readonly FixedWidthLayout<TModel> _layout;

        /// <summary>Builds the parser, throwing <see cref="InvalidOperationException"/> when the model's layout is invalid.</summary>
        [RequiresUnreferencedCode("Attribute layouts reflect over TModel's public properties and fields, which trimming may remove.")]
        [RequiresDynamicCode("Attribute layouts compile member accessors at runtime (Expression.Compile / MakeGenericType).")]
        public FixedWidthParser()
        {
            _layout = FixedWidthLayout.FromAttributes<TModel>();
            _layout.EnsureCanRead();
        }

        /// <summary>
        /// Parses a single line, without its line break, into <paramref name="model"/>. Returns
        /// <see langword="false"/> when the line is shorter than the layout or a non-string column fails
        /// to parse, after reporting why through <paramref name="context"/>.
        /// </summary>
        public bool TryParse(ReadOnlySpan<byte> line, in FixedWidthContext context, out TModel model)
        {
            return _layout.TryParse(line, in context, out model);
        }
    }
}
