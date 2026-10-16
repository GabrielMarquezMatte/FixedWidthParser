using System.IO.Pipelines;
using FixedWidthParser.Parsers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Reads attribute-mapped models from a stream, a file or a pipe, lazily and with low allocation,
    /// through the runtime (reflection-built) parser. Configured once; reusable across reads. For
    /// source-generated models use <see cref="FixedWidth"/>.
    /// <para><b>Column positions are bytes.</b></para>
    /// </summary>
    public sealed class FixedWidthReader<TModel> where TModel : new()
    {
        private readonly FixedWidthParser<TModel> _parser = new();
        private readonly FixedWidthReaderOptions _options;
        private readonly FixedWidthContext _context;

        /// <summary>Creates a reader; <see langword="null"/> options use every default.</summary>
        public FixedWidthReader(FixedWidthReaderOptions? options = null)
        {
            _options = FixedWidthReaderOptions.Validate(options);
            _context = _options.Context;
        }

        /// <summary>Parses a single line, without its line break, using the reader's options.</summary>
        public bool TryParse(ReadOnlySpan<byte> line, out TModel model)
        {
            return _parser.TryParse(line, in _context, out model);
        }

        /// <summary>Reads from a stream, once. <paramref name="leaveOpen"/> controls whether the stream is disposed when iteration ends.</summary>
        public FixedWidthRecordEnumerable<TModel, ReflectionLineParser<TModel>> Read(Stream stream, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return new(new ReflectionLineParser<TModel>(_parser), stream, ownsStream: !leaveOpen, _options);
        }

        /// <summary>Reads from a file, reopening it on each enumeration.</summary>
        public FixedWidthRecordEnumerable<TModel, ReflectionLineParser<TModel>> ReadFile(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return new(new ReflectionLineParser<TModel>(_parser), path, _options);
        }

        /// <summary>Reads from a stream via <c>await foreach</c>, once. <paramref name="leaveOpen"/> controls whether the stream is disposed when iteration ends.</summary>
        public FixedWidthAsyncRecordEnumerable<TModel, ReflectionLineParser<TModel>> ReadAsync(Stream stream, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return new(new ReflectionLineParser<TModel>(_parser), stream, ownsStream: !leaveOpen, _options);
        }

        /// <summary>
        /// Reads from a <see cref="PipeReader"/> via <c>await foreach</c>, letting the pipe own buffering
        /// (<see cref="FixedWidthReaderOptions.BufferSize"/> does not apply). Use it when the source is
        /// already a pipe, such as a Kestrel request body. <paramref name="leaveOpen"/> controls whether
        /// the reader is completed when iteration ends.
        /// </summary>
        public FixedWidthPipeRecordEnumerable<TModel, ReflectionLineParser<TModel>> ReadAsync(PipeReader reader, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(reader);
            return new(new ReflectionLineParser<TModel>(_parser), reader, completeReader: !leaveOpen, _options);
        }

        /// <summary>Reads from a file via <c>await foreach</c> with asynchronous I/O, reopening it on each enumeration.</summary>
        public FixedWidthAsyncRecordEnumerable<TModel, ReflectionLineParser<TModel>> ReadFileAsync(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return new(new ReflectionLineParser<TModel>(_parser), path, _options);
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
        public Task<TAccumulator> AggregateFileAsync<TAccumulator>(string path, FixedWidthParallelOptions? parallel = null, CancellationToken ct = default)
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return ParallelAggregator.RunAsync<TModel, ReflectionLineParser<TModel>, TAccumulator>(
                new ReflectionLineParser<TModel>(_parser), path, _options, FixedWidthParallelOptions.Validate(parallel), ct);
        }

        /// <summary>
        /// Reads a stream once and folds every record into a <typeparamref name="TAccumulator"/> across
        /// several threads: one thread reads the stream into chunks, the others parse them, one instance
        /// per chunk, merged in stream order. <paramref name="leaveOpen"/> controls whether the stream is
        /// disposed afterwards.
        /// </summary>
        /// <remarks>Carries the same contract as <see cref="AggregateFileAsync{TAccumulator}"/>.</remarks>
        public Task<TAccumulator> AggregateAsync<TAccumulator>(Stream stream, bool leaveOpen = false, FixedWidthParallelOptions? parallel = null, CancellationToken ct = default)
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            ArgumentNullException.ThrowIfNull(stream);
            return ParallelStreamAggregator.RunAsync<TModel, ReflectionLineParser<TModel>, TAccumulator>(
                new ReflectionLineParser<TModel>(_parser), stream, leaveOpen, _options, FixedWidthParallelOptions.Validate(parallel), ct);
        }
    }
}
