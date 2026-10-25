using System.Diagnostics.CodeAnalysis;
using FixedWidthParser.Infrastructure;

namespace FixedWidthParser.Processors
{
    /// <summary>
    /// Parses the already-sliced bytes of a single column and assigns the value to the model. Returns
    /// <see langword="false"/> to reject the line.
    /// </summary>
    internal delegate bool ColumnParser<TModel>(ReadOnlySpan<byte> column, in FixedWidthContext context, ref TModel model)
        where TModel : allows ref struct;

    /// <summary>Parses the already-sliced bytes of a single column: the shape of the <see cref="FixedWidthRuntime"/> parse helpers.</summary>
    internal delegate bool ColumnValueParser<TValue>(ReadOnlySpan<byte> column, in FixedWidthContext context, in ColumnParseOptions options, [MaybeNullWhen(false)] out TValue value);
}
