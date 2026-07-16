using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Processors
{
    /// <summary>
    /// UTF-8 counterpart of <see cref="ColumnParser{TModel}"/>: parses the already-sliced
    /// <em>bytes</em> of a single column and assigns it to the model. The byte parser slices each
    /// column out of the raw UTF-8 line (honoring start/length, measured in <b>bytes</b>) and hands
    /// the resulting span here, so this delegate never deals with offsets. Returns
    /// <see langword="false"/> to reject the line.
    /// <para>
    /// A <see cref="StringPool"/> may be supplied to intern string columns: they are decoded via
    /// <see cref="StringPool.GetOrAdd(ReadOnlySpan{byte}, System.Text.Encoding)"/> (UTF-8), so a
    /// repeated value is decoded once and the same <see cref="string"/> instance is reused.
    /// </para>
    /// </summary>
    public delegate bool Utf8ColumnParser<TModel>(
        ReadOnlySpan<byte> column,
        IFormatProvider? formatProvider,
        StringPool? stringPool,
#if NET9_0_OR_GREATER
        ref TModel model) where TModel : allows ref struct;
#else
        ref TModel model);
#endif
}
