using FixedWidthParser.Infrastructure;

namespace FixedWidthParser.Writers
{
    /// <summary>
    /// Format strategy <see cref="RecordWriter"/> is specialized by, the write-side twin of
    /// <see cref="Infrastructure.IRecordLineParser{TModel}"/>: implemented by <see langword="struct"/> types so each
    /// <c>(TModel, TFormatter)</c> pair is compiled once and the call devirtualizes.
    /// </summary>
    internal interface IRecordFormatter<TModel>
        where TModel : allows ref struct
    {
        /// <summary>Formats one line, without its line break. Returns <see langword="false"/> only when <paramref name="destination"/> is shorter than the line.</summary>
        bool TryFormat(in TModel model, Span<byte> destination, in FixedWidthContext context, out int bytesWritten);
    }

    internal readonly struct LayoutRecordFormatter<TModel>(FixedWidthLayout<TModel> layout) : IRecordFormatter<TModel>
        where TModel : allows ref struct
    {
        public bool TryFormat(in TModel model, Span<byte> destination, in FixedWidthContext context, out int bytesWritten)
        {
            int lineLength = layout.LineLength;
            if (destination.Length < lineLength)
            {
                bytesWritten = 0;
                return false;
            }
            layout.FormatLine(in model, destination[..lineLength], in context);
            bytesWritten = lineLength;
            return true;
        }
    }

    internal readonly struct GeneratedRecordFormatter<TModel> : IRecordFormatter<TModel>
        where TModel : IFixedWidthModel<TModel>
    {
        public bool TryFormat(in TModel model, Span<byte> destination, in FixedWidthContext context, out int bytesWritten)
        {
            return TModel.TryFormat(in model, destination, in context, out bytesWritten);
        }
    }
}
