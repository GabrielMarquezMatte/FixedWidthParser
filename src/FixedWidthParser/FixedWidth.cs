using FixedWidthParser.Infrastructure;
using FixedWidthParser.Readers;
using FixedWidthParser.Writers;

namespace FixedWidthParser
{
    /// <summary>
    /// Parses, reads, aggregates, formats and writes fixed-width lines. Every operation comes in two
    /// overloads: one for a source-generated model (<see cref="IFixedWidthModel{TSelf}"/>), resolved
    /// statically through the type parameter, and one that takes a <see cref="FixedWidthLayout{TModel}"/>
    /// built from attributes or fluently.
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
            return WritableLayout(layout).TryFormat(in model, destination, in context, out bytesWritten);
        }

        /// <summary>Reads from a stream, once. <paramref name="leaveOpen"/> controls whether the stream is disposed when iteration ends.</summary>
        public static FixedWidthRecordEnumerable<TModel, GeneratedLineParser<TModel>> Read<TModel>(Stream stream, bool leaveOpen = false, FixedWidthReaderOptions? options = null)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentNullException.ThrowIfNull(stream);
            return new(default, stream, ownsStream: !leaveOpen, FixedWidthReaderOptions.Validate(options));
        }

        /// <summary>Reads from a stream through <paramref name="layout"/>, once. <paramref name="leaveOpen"/> controls whether the stream is disposed when iteration ends.</summary>
        /// <exception cref="InvalidOperationException"><paramref name="layout"/> has a column without a setter.</exception>
        public static FixedWidthRecordEnumerable<TModel, LayoutLineParser<TModel>> Read<TModel>(Stream stream, FixedWidthLayout<TModel> layout, bool leaveOpen = false, FixedWidthReaderOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return new(ReadableLayout(layout), stream, ownsStream: !leaveOpen, FixedWidthReaderOptions.Validate(options));
        }

        /// <summary>Reads from a file, reopening it on each enumeration.</summary>
        public static FixedWidthRecordEnumerable<TModel, GeneratedLineParser<TModel>> ReadFile<TModel>(string path, FixedWidthReaderOptions? options = null)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return new(default, path, FixedWidthReaderOptions.Validate(options));
        }

        /// <summary>Reads from a file through <paramref name="layout"/>, reopening it on each enumeration.</summary>
        /// <exception cref="InvalidOperationException"><paramref name="layout"/> has a column without a setter.</exception>
        public static FixedWidthRecordEnumerable<TModel, LayoutLineParser<TModel>> ReadFile<TModel>(string path, FixedWidthLayout<TModel> layout, FixedWidthReaderOptions? options = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return new(ReadableLayout(layout), path, FixedWidthReaderOptions.Validate(options));
        }

        /// <summary>Reads from a stream via <c>await foreach</c>, once. <paramref name="leaveOpen"/> controls whether the stream is disposed when iteration ends.</summary>
        public static FixedWidthAsyncRecordEnumerable<TModel, GeneratedLineParser<TModel>> ReadAsync<TModel>(Stream stream, bool leaveOpen = false, FixedWidthReaderOptions? options = null)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentNullException.ThrowIfNull(stream);
            return new(default, stream, ownsStream: !leaveOpen, FixedWidthReaderOptions.Validate(options));
        }

        /// <summary>Reads from a stream through <paramref name="layout"/> via <c>await foreach</c>, once. <paramref name="leaveOpen"/> controls whether the stream is disposed when iteration ends.</summary>
        /// <exception cref="InvalidOperationException"><paramref name="layout"/> has a column without a setter.</exception>
        public static FixedWidthAsyncRecordEnumerable<TModel, LayoutLineParser<TModel>> ReadAsync<TModel>(Stream stream, FixedWidthLayout<TModel> layout, bool leaveOpen = false, FixedWidthReaderOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return new(ReadableLayout(layout), stream, ownsStream: !leaveOpen, FixedWidthReaderOptions.Validate(options));
        }

        /// <summary>Reads from a file via <c>await foreach</c> with asynchronous I/O, reopening it on each enumeration.</summary>
        public static FixedWidthAsyncRecordEnumerable<TModel, GeneratedLineParser<TModel>> ReadFileAsync<TModel>(string path, FixedWidthReaderOptions? options = null)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return new(default, path, FixedWidthReaderOptions.Validate(options));
        }

        /// <summary>Reads from a file through <paramref name="layout"/> via <c>await foreach</c> with asynchronous I/O, reopening it on each enumeration.</summary>
        /// <exception cref="InvalidOperationException"><paramref name="layout"/> has a column without a setter.</exception>
        public static FixedWidthAsyncRecordEnumerable<TModel, LayoutLineParser<TModel>> ReadFileAsync<TModel>(string path, FixedWidthLayout<TModel> layout, FixedWidthReaderOptions? options = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return new(ReadableLayout(layout), path, FixedWidthReaderOptions.Validate(options));
        }

        /// <summary>
        /// Reads a file across several threads and folds every record into a <typeparamref name="TAccumulator"/>,
        /// one instance per partition, merged in file order.
        /// </summary>
        /// <remarks>
        /// The result equals one sequential pass when <see cref="IRecordAccumulator{TSelf, TModel}.Merge"/> is
        /// associative. Every line is parsed exactly once, and errors report the same line number as a
        /// sequential read. <see cref="FixedWidthReaderOptions.OnParseError"/> runs on the worker threads,
        /// possibly concurrently, and the first failure in file order is the one thrown.
        /// </remarks>
        public static Task<TAccumulator> AggregateFileAsync<TModel, TAccumulator>(
            string path, FixedWidthReaderOptions? options = null, CancellationToken cancellationToken = default)
            where TModel : IFixedWidthModel<TModel>
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return ParallelAggregator.RunAsync<TModel, GeneratedLineParser<TModel>, TAccumulator>(
                default, path, FixedWidthReaderOptions.Validate(options), cancellationToken);
        }

        /// <summary>
        /// Reads a file through <paramref name="layout"/> across several threads and folds every record into
        /// a <typeparamref name="TAccumulator"/>, one instance per partition, merged in file order.
        /// </summary>
        /// <remarks>Carries the same contract as <see cref="AggregateFileAsync{TModel, TAccumulator}(string, FixedWidthReaderOptions?, CancellationToken)"/>.</remarks>
        /// <exception cref="InvalidOperationException"><paramref name="layout"/> has a column without a setter.</exception>
        public static Task<TAccumulator> AggregateFileAsync<TModel, TAccumulator>(
            string path, FixedWidthLayout<TModel> layout, FixedWidthReaderOptions? options = null, CancellationToken cancellationToken = default)
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return ParallelAggregator.RunAsync<TModel, LayoutLineParser<TModel>, TAccumulator>(
                ReadableLayout(layout), path, FixedWidthReaderOptions.Validate(options), cancellationToken);
        }

        /// <summary>
        /// Reads a stream once and folds every record into a <typeparamref name="TAccumulator"/> across
        /// several threads. <paramref name="leaveOpen"/> controls whether the stream is disposed afterwards.
        /// </summary>
        /// <remarks>Carries the same contract as <see cref="AggregateFileAsync{TModel, TAccumulator}(string, FixedWidthReaderOptions?, CancellationToken)"/>.</remarks>
        public static Task<TAccumulator> AggregateAsync<TModel, TAccumulator>(
            Stream stream, bool leaveOpen = false, FixedWidthReaderOptions? options = null, CancellationToken cancellationToken = default)
            where TModel : IFixedWidthModel<TModel>
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            ArgumentNullException.ThrowIfNull(stream);
            return ParallelStreamAggregator.RunAsync<TModel, GeneratedLineParser<TModel>, TAccumulator>(
                default, stream, leaveOpen, FixedWidthReaderOptions.Validate(options), cancellationToken);
        }

        /// <summary>
        /// Reads a stream once through <paramref name="layout"/> and folds every record into a
        /// <typeparamref name="TAccumulator"/> across several threads. <paramref name="leaveOpen"/> controls
        /// whether the stream is disposed afterwards.
        /// </summary>
        /// <remarks>Carries the same contract as <see cref="AggregateFileAsync{TModel, TAccumulator}(string, FixedWidthReaderOptions?, CancellationToken)"/>.</remarks>
        /// <exception cref="InvalidOperationException"><paramref name="layout"/> has a column without a setter.</exception>
        public static Task<TAccumulator> AggregateAsync<TModel, TAccumulator>(
            Stream stream, FixedWidthLayout<TModel> layout, bool leaveOpen = false, FixedWidthReaderOptions? options = null, CancellationToken cancellationToken = default)
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            ArgumentNullException.ThrowIfNull(stream);
            return ParallelStreamAggregator.RunAsync<TModel, LayoutLineParser<TModel>, TAccumulator>(
                ReadableLayout(layout), stream, leaveOpen, FixedWidthReaderOptions.Validate(options), cancellationToken);
        }


        /// <summary>Writes one record and its line break.</summary>
        /// <exception cref="InvalidOperationException">A value does not fit a column whose overflow throws.</exception>
        public static void Write<TModel>(Stream stream, in TModel model, FixedWidthWriterOptions? options = null)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentNullException.ThrowIfNull(stream);
            RecordWriter.Write(stream, in model, default(GeneratedRecordFormatter<TModel>), FixedWidthWriterOptions.Validate(options));
        }

        /// <summary>Writes one record and its line break through <paramref name="layout"/>.</summary>
        /// <exception cref="InvalidOperationException"><paramref name="layout"/> has a column without a getter, or a value does not fit a column whose overflow throws.</exception>
        public static void Write<TModel>(Stream stream, in TModel model, FixedWidthLayout<TModel> layout, FixedWidthWriterOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(stream);
            RecordWriter.Write(stream, in model, WritableLayout(layout), FixedWidthWriterOptions.Validate(options));
        }

        /// <summary>Writes every record, each followed by a line break.</summary>
        /// <exception cref="InvalidOperationException">A value does not fit a column whose overflow throws.</exception>
        public static void WriteMany<TModel>(Stream stream, IEnumerable<TModel> models, FixedWidthWriterOptions? options = null)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(models);
            RecordWriter.WriteMany(stream, models, default(GeneratedRecordFormatter<TModel>), FixedWidthWriterOptions.Validate(options));
        }

        /// <summary>Writes every record through <paramref name="layout"/>, each followed by a line break.</summary>
        /// <exception cref="InvalidOperationException"><paramref name="layout"/> has a column without a getter, or a value does not fit a column whose overflow throws.</exception>
        public static void WriteMany<TModel>(Stream stream, IEnumerable<TModel> models, FixedWidthLayout<TModel> layout, FixedWidthWriterOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(models);
            RecordWriter.WriteMany(stream, models, WritableLayout(layout), FixedWidthWriterOptions.Validate(options));
        }

        /// <summary>Writes every record, each followed by a line break, reading each one by reference.</summary>
        /// <exception cref="InvalidOperationException">A value does not fit a column whose overflow throws.</exception>
        public static void WriteMany<TModel>(Stream stream, ReadOnlySpan<TModel> models, FixedWidthWriterOptions? options = null)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentNullException.ThrowIfNull(stream);
            RecordWriter.WriteMany(stream, models, default(GeneratedRecordFormatter<TModel>), FixedWidthWriterOptions.Validate(options));
        }

        /// <summary>Writes every record through <paramref name="layout"/>, each followed by a line break, reading each one by reference.</summary>
        /// <exception cref="InvalidOperationException"><paramref name="layout"/> has a column without a getter, or a value does not fit a column whose overflow throws.</exception>
        public static void WriteMany<TModel>(Stream stream, ReadOnlySpan<TModel> models, FixedWidthLayout<TModel> layout, FixedWidthWriterOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(stream);
            RecordWriter.WriteMany(stream, models, WritableLayout(layout), FixedWidthWriterOptions.Validate(options));
        }

        /// <summary>Writes one record and its line break.</summary>
        /// <exception cref="InvalidOperationException">A value does not fit a column whose overflow throws.</exception>
        public static Task WriteAsync<TModel>(Stream stream, TModel model, FixedWidthWriterOptions? options = null, CancellationToken cancellationToken = default)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentNullException.ThrowIfNull(stream);
            return RecordWriter.WriteAsync(stream, model, default(GeneratedRecordFormatter<TModel>), FixedWidthWriterOptions.Validate(options), cancellationToken);
        }

        /// <summary>Writes one record and its line break through <paramref name="layout"/>.</summary>
        /// <exception cref="InvalidOperationException"><paramref name="layout"/> has a column without a getter, or a value does not fit a column whose overflow throws.</exception>
        public static Task WriteAsync<TModel>(Stream stream, TModel model, FixedWidthLayout<TModel> layout, FixedWidthWriterOptions? options = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return RecordWriter.WriteAsync(stream, model, WritableLayout(layout), FixedWidthWriterOptions.Validate(options), cancellationToken);
        }

        /// <summary>Writes every record, each followed by a line break.</summary>
        /// <exception cref="InvalidOperationException">A value does not fit a column whose overflow throws.</exception>
        public static Task WriteManyAsync<TModel>(Stream stream, IEnumerable<TModel> models, FixedWidthWriterOptions? options = null, CancellationToken cancellationToken = default)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(models);
            return RecordWriter.WriteManyAsync(stream, models, default(GeneratedRecordFormatter<TModel>), FixedWidthWriterOptions.Validate(options), cancellationToken);
        }

        /// <summary>Writes every record through <paramref name="layout"/>, each followed by a line break.</summary>
        /// <exception cref="InvalidOperationException"><paramref name="layout"/> has a column without a getter, or a value does not fit a column whose overflow throws.</exception>
        public static Task WriteManyAsync<TModel>(Stream stream, IEnumerable<TModel> models, FixedWidthLayout<TModel> layout, FixedWidthWriterOptions? options = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(models);
            return RecordWriter.WriteManyAsync(stream, models, WritableLayout(layout), FixedWidthWriterOptions.Validate(options), cancellationToken);
        }

        private static LayoutRecordFormatter<TModel> WritableLayout<TModel>(FixedWidthLayout<TModel> layout)
            where TModel : allows ref struct
        {
            ArgumentNullException.ThrowIfNull(layout);
            layout.EnsureCanWrite();
            return new LayoutRecordFormatter<TModel>(layout);
        }

        private static LayoutLineParser<TModel> ReadableLayout<TModel>(FixedWidthLayout<TModel> layout)
        {
            ArgumentNullException.ThrowIfNull(layout);
            layout.EnsureCanRead();
            return new LayoutLineParser<TModel>(layout);
        }
    }
}
