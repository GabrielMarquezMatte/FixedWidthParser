using FixedWidthParser.Readers;

namespace FixedWidthParser
{
    /// <summary>
    /// Entry points for source-generated models. Dispatches to the model's generated
    /// <see cref="IFixedWidthModel{TSelf}.TryParse"/> and <see cref="IFixedWidthModel{TSelf}.TryFormat"/>
    /// through the type parameter, so the call is statically resolved (no reflection, no delegates).
    /// <para><b>Column positions are bytes.</b></para>
    /// </summary>
    public static class FixedWidth
    {
        /// <summary>Parses a single line, without its line break, into <typeparamref name="TModel"/>.</summary>
        public static bool TryParse<TModel>(ReadOnlySpan<byte> line, in FixedWidthContext context, out TModel model)
            where TModel : IFixedWidthModel<TModel>, allows ref struct
        {
            return TModel.TryParse(line, in context, out model);
        }

        /// <summary>
        /// Parses a single line, without its line break, into <typeparamref name="TModel"/> through
        /// <paramref name="layout"/>. Returns <see langword="false"/> when the line is shorter than the layout
        /// or a non-string column fails to parse, after reporting why through <paramref name="context"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException"><paramref name="layout"/> has a column without a setter.</exception>
        public static bool TryParse<TModel>(ReadOnlySpan<byte> line, FixedWidthLayout<TModel> layout, in FixedWidthContext context, out TModel model)
            where TModel : allows ref struct
        {
            ArgumentNullException.ThrowIfNull(layout);
            layout.EnsureCanRead();
            return layout.TryParse(line, in context, out model);
        }

        /// <summary>Formats <paramref name="model"/> into <paramref name="destination"/>, without a line break.</summary>
        public static bool TryFormat<TModel>(in TModel model, Span<byte> destination, in FixedWidthContext context, out int bytesWritten)
            where TModel : IFixedWidthModel<TModel>, allows ref struct
        {
            return TModel.TryFormat(in model, destination, in context, out bytesWritten);
        }

        /// <summary>
        /// Formats <paramref name="model"/> into <paramref name="destination"/> through <paramref name="layout"/>,
        /// without a line break. Returns <see langword="false"/> only when <paramref name="destination"/> is
        /// shorter than the line.
        /// </summary>
        /// <exception cref="InvalidOperationException"><paramref name="layout"/> has a column without a getter, or a value does not fit a column whose overflow throws.</exception>
        public static bool TryFormat<TModel>(in TModel model, FixedWidthLayout<TModel> layout, Span<byte> destination, in FixedWidthContext context, out int bytesWritten)
            where TModel : allows ref struct
        {
            ArgumentNullException.ThrowIfNull(layout);
            layout.EnsureCanWrite();
            if (destination.Length < layout.LineLength)
            {
                bytesWritten = 0;
                return false;
            }
            layout.FormatLine(in model, destination[..layout.LineLength], in context);
            bytesWritten = layout.LineLength;
            return true;
        }

        /// <summary>Reads from a stream, once. <paramref name="leaveOpen"/> controls whether the stream is disposed when iteration ends.</summary>
        public static FixedWidthRecordEnumerable<TModel, GeneratedLineParser<TModel>> Read<TModel>(Stream stream, bool leaveOpen = false, FixedWidthReaderOptions? options = null)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentNullException.ThrowIfNull(stream);
            return new(default, stream, ownsStream: !leaveOpen, FixedWidthReaderOptions.Validate(options));
        }

        /// <summary>Reads from a file, reopening it on each enumeration.</summary>
        public static FixedWidthRecordEnumerable<TModel, GeneratedLineParser<TModel>> ReadFile<TModel>(string path, FixedWidthReaderOptions? options = null)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return new(default, path, FixedWidthReaderOptions.Validate(options));
        }

        /// <summary>Reads from a stream via <c>await foreach</c>, once. <paramref name="leaveOpen"/> controls whether the stream is disposed when iteration ends.</summary>
        public static FixedWidthAsyncRecordEnumerable<TModel, GeneratedLineParser<TModel>> ReadAsync<TModel>(Stream stream, bool leaveOpen = false, FixedWidthReaderOptions? options = null)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentNullException.ThrowIfNull(stream);
            return new(default, stream, ownsStream: !leaveOpen, FixedWidthReaderOptions.Validate(options));
        }

        /// <summary>Reads from a file via <c>await foreach</c> with asynchronous I/O, reopening it on each enumeration.</summary>
        public static FixedWidthAsyncRecordEnumerable<TModel, GeneratedLineParser<TModel>> ReadFileAsync<TModel>(string path, FixedWidthReaderOptions? options = null)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return new(default, path, FixedWidthReaderOptions.Validate(options));
        }

        /// <summary>
        /// Reads a file across several threads and folds every record into a <typeparamref name="TAccumulator"/>,
        /// one instance per partition, merged in file order.
        /// </summary>
        /// <remarks>Carries the same contract as <see cref="FixedWidthReader{TModel}.AggregateFileAsync{TAccumulator}"/>.</remarks>
        public static Task<TAccumulator> AggregateFileAsync<TModel, TAccumulator>(
            string path, FixedWidthReaderOptions? options = null, FixedWidthParallelOptions? parallel = null, CancellationToken ct = default)
            where TModel : IFixedWidthModel<TModel>
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return ParallelAggregator.RunAsync<TModel, GeneratedLineParser<TModel>, TAccumulator>(
                default, path, FixedWidthReaderOptions.Validate(options), FixedWidthParallelOptions.Validate(parallel), ct);
        }

        /// <summary>
        /// Reads a stream once and folds every record into a <typeparamref name="TAccumulator"/> across
        /// several threads. <paramref name="leaveOpen"/> controls whether the stream is disposed afterwards.
        /// </summary>
        /// <remarks>Carries the same contract as <see cref="FixedWidthReader{TModel}.AggregateAsync{TAccumulator}"/>.</remarks>
        public static Task<TAccumulator> AggregateAsync<TModel, TAccumulator>(
            Stream stream, bool leaveOpen = false, FixedWidthReaderOptions? options = null, FixedWidthParallelOptions? parallel = null, CancellationToken ct = default)
            where TModel : IFixedWidthModel<TModel>
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            ArgumentNullException.ThrowIfNull(stream);
            return ParallelStreamAggregator.RunAsync<TModel, GeneratedLineParser<TModel>, TAccumulator>(
                default, stream, leaveOpen, FixedWidthReaderOptions.Validate(options), FixedWidthParallelOptions.Validate(parallel), ct);
        }
    }
}
