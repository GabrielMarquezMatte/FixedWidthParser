using System.IO.Pipelines;
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

        /// <summary>Formats <paramref name="model"/> into <paramref name="destination"/>, without a line break.</summary>
        public static bool TryFormat<TModel>(in TModel model, Span<byte> destination, in FixedWidthContext context, out int bytesWritten)
            where TModel : IFixedWidthModel<TModel>, allows ref struct
        {
            return TModel.TryFormat(in model, destination, in context, out bytesWritten);
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

        /// <summary>
        /// Reads from a <see cref="PipeReader"/> via <c>await foreach</c>, letting the pipe own buffering.
        /// <paramref name="leaveOpen"/> controls whether the reader is completed when iteration ends.
        /// </summary>
        public static FixedWidthPipeRecordEnumerable<TModel, GeneratedLineParser<TModel>> ReadAsync<TModel>(PipeReader reader, bool leaveOpen = false, FixedWidthReaderOptions? options = null)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentNullException.ThrowIfNull(reader);
            return new(default, reader, completeReader: !leaveOpen, FixedWidthReaderOptions.Validate(options));
        }

        /// <summary>Reads from a file via <c>await foreach</c> with asynchronous I/O, reopening it on each enumeration.</summary>
        public static FixedWidthAsyncRecordEnumerable<TModel, GeneratedLineParser<TModel>> ReadFileAsync<TModel>(string path, FixedWidthReaderOptions? options = null)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return new(default, path, FixedWidthReaderOptions.Validate(options));
        }
    }
}
