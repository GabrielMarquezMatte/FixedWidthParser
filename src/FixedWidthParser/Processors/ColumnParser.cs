using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Processors
{
    /// <summary>
    /// Parses the already-sliced text of a single column and assigns it to the model. The parser
    /// slices each column out of the line (honoring start/length) and hands the resulting span here,
    /// so this delegate never deals with offsets. Replaces the old <c>IColumnProcessor</c>
    /// abstraction: one delegate per column. Returns <see langword="false"/> to reject the line.
    /// </summary>
    public delegate bool ColumnParser<TModel>(
        ReadOnlySpan<char> column,
        IFormatProvider? formatProvider,
        StringPool? stringPool,
        ref TModel model) where TModel : allows ref struct;

    /// <summary>
    /// Parses the (already sliced, not trimmed) text of a single column into a value of type
    /// <typeparamref name="TValue"/>. This is the unit of extensibility: register one of these per
    /// type with <see cref="ColumnParserRegistry"/> to teach the parser a new column type without
    /// implementing a class. Returns <see langword="false"/> when the text is not a valid value.
    /// </summary>
    public delegate bool ColumnValueParser<TValue>(
        ReadOnlySpan<char> span,
        IFormatProvider? formatProvider,
        [MaybeNullWhen(false)] out TValue value);
}
