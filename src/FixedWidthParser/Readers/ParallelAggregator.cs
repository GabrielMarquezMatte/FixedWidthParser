using System.Buffers;
using System.Runtime.ExceptionServices;
using FixedWidthParser.Infrastructure;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Reads a file in byte-range partitions on several threads, folds each partition into its own
    /// accumulator, and merges them in file order. A line belongs to the partition its first byte falls
    /// in: a worker starts one byte before its range, skips the line it lands inside, and stops once the
    /// next line would start past its range. Fixed-width lines have no quoting, so that boundary is exact
    /// and every line is parsed exactly once. One partition is the sequential read.
    /// </summary>
    internal static class ParallelAggregator
    {
        private const int ChunksPerWorker = 4;
        private const long MinChunkBytes = 1024 * 1024;
        private const long MaxChunkBytes = 64 * 1024 * 1024;
        private const int LinesPerCancellationCheck = 4096;

        internal static async Task<TAccumulator> RunAsync<TModel, TParser, TAccumulator>(
            TParser parser, string path, FixedWidthReaderOptions options, CancellationToken ct)
            where TModel : allows ref struct
            where TParser : struct, IRecordLineParser<TModel>
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var source = new FileSource(handle);
            return await RunPartitionsAsync(source, 0, options,
                (plan, index) => AggregateChunk<TModel, TParser, TAccumulator>(parser, source, plan, index, options, ct),
                static (merged, following) =>
                {
                    merged.Merge(following);
                    return merged;
                }, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads <c>[dataStart, source.Length)</c> in partitions, one <typeparamref name="TSink"/> each, merged in
        /// order. <paramref name="dataStart"/> must start a line; line numbers count from the start of the source.
        /// <paramref name="create"/> gets where the bytes of the partition its sink will read start and end; a line
        /// belongs to the partition its first byte falls in.
        /// </summary>
        internal static Task<TSink> RunAsync<TSink>(
            IRandomAccessSource source, long dataStart, Func<long, long, TSink> create, FixedWidthReaderOptions options, CancellationToken ct)
            where TSink : struct, ILineSink<TSink>
        {
            return RunPartitionsAsync(source, dataStart, options,
                (plan, index) => SinkChunk(source, plan, index, create(plan.Start(index), plan.End(index)), options, ct),
                static (merged, following) =>
                {
                    merged.Merge(following);
                    return merged;
                }, ct);
        }

        private static async Task<TResult> RunPartitionsAsync<TResult>(
            IRandomAccessSource source, long dataStart, FixedWidthReaderOptions options,
            Func<ChunkPlan, int, TResult> aggregate, Func<TResult, TResult, TResult> merge, CancellationToken ct)
        {
            var plan = new ChunkPlan(dataStart, source.Length, options);
            var results = new TResult[plan.Count];
            var failures = new ExceptionDispatchInfo?[plan.Count];
            var workers = new Task[Math.Min(options.Workers, plan.Count)];
            foreach (ref var worker in workers.AsSpan())
            {
                worker = Task.Run(() => Drain(plan, aggregate, results, failures, ct), ct);
            }
            await Task.WhenAll(workers).ConfigureAwait(false);

            failures[0]?.Throw();
            var merged = results[0];
            for (int i = 1; i < plan.Count; i++)
            {
                failures[i]?.Throw();
                merged = merge(merged, results[i]);
            }
            return merged;
        }

        private static void Drain<TResult>(
            ChunkPlan plan, Func<ChunkPlan, int, TResult> aggregate, TResult[] results, ExceptionDispatchInfo?[] failures, CancellationToken ct)
        {
            while (plan.TryTakeNext(out int index))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    results[index] = aggregate(plan, index);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failures[index] = ExceptionDispatchInfo.Capture(ex);
                }
            }
        }

        // ponytail: the model and sink loops are twins on purpose. Folding the model path into a ModelSink
        // made one-call reads ~40% slower: dynamic PGO stopped devirtualizing IRecordAccumulator.Add through
        // the shared generic sink. Keep them in step.
        private static TAccumulator AggregateChunk<TModel, TParser, TAccumulator>(
            TParser parser, IRandomAccessSource source, ChunkPlan plan, int index, FixedWidthReaderOptions options, CancellationToken ct)
            where TModel : allows ref struct
            where TParser : struct, IRecordLineParser<TModel>
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            long start = plan.Start(index);
            long end = plan.End(index);
            var accumulator = new TAccumulator();
            var context = options.Context;
            var reader = new ChunkReader(source, index == 0 ? start : start - 1, options.MaxLineLength);
            var lines = default(LineBufferState);
            lines.Rent(options, start == 0 && options.SkipsByteOrderMark);
            long delivered = 0;
            try
            {
                while (reader.Origin + lines.Consumed < end)
                {
                    if (reader.NextLine(ref lines, out var line) == LineStatus.End)
                    {
                        break;
                    }
                    long lineStart = reader.Origin + lines.LineStart;
                    if (lineStart < start || lineStart >= end)
                    {
                        continue;
                    }
                    if (++delivered % LinesPerCancellationCheck == 0)
                    {
                        ct.ThrowIfCancellationRequested();
                    }
                    if (!TryAdd<TModel, TParser, TAccumulator>(parser, line, in context, ref accumulator))
                    {
                        ParseErrors.HandleFailure<TModel, TParser>(parser, line, reader.LineNumberAt(lineStart), in context, options.OnParseError);
                    }
                }
            }
            finally
            {
                lines.Return();
            }
            return accumulator;
        }

        private static TSink SinkChunk<TSink>(
            IRandomAccessSource source, ChunkPlan plan, int index, TSink sink, FixedWidthReaderOptions options, CancellationToken ct)
            where TSink : struct, ILineSink<TSink>
        {
            long start = plan.Start(index);
            long end = plan.End(index);
            var context = options.Context;
            var reader = new ChunkReader(source, index == 0 ? start : start - 1, options.MaxLineLength);
            var lines = default(LineBufferState);
            lines.Rent(options, start == 0 && options.SkipsByteOrderMark);
            long delivered = 0;
            try
            {
                while (reader.Origin + lines.Consumed < end)
                {
                    if (reader.NextLine(ref lines, out var line) == LineStatus.End)
                    {
                        break;
                    }
                    long lineStart = reader.Origin + lines.LineStart;
                    if (lineStart < start || lineStart >= end)
                    {
                        continue;
                    }
                    if (++delivered % LinesPerCancellationCheck == 0)
                    {
                        ct.ThrowIfCancellationRequested();
                    }
                    if (!sink.TryAdd(line, in context))
                    {
                        if (options.OnSkip is { } skip)
                        {
                            skip();
                            continue;
                        }
                        ParseErrors.Reject(sink.Diagnose(line, reader.LineNumberAt(lineStart), in context), options.OnParseError);
                    }
                }
            }
            finally
            {
                lines.Return();
            }
            return sink;
        }

        /// <summary>Parses one line and folds it in; <see langword="false"/> when it does not parse.</summary>
        internal static bool TryAdd<TModel, TParser, TAccumulator>(TParser parser, ReadOnlySpan<byte> line, in FixedWidthContext context, ref TAccumulator accumulator)
            where TModel : allows ref struct
            where TParser : struct, IRecordLineParser<TModel>
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>
        {
            if (!parser.TryParse(line, in context, out var model))
            {
                return false;
            }
            accumulator.Add(in model);
            return true;
        }

        private sealed class ChunkPlan
        {
            private readonly long _dataStart;
            private readonly long _length;
            private readonly long _chunkSize;
            private int _next = -1;

            internal ChunkPlan(long dataStart, long length, FixedWidthReaderOptions options)
            {
                _dataStart = dataStart;
                _length = length;
                long data = Math.Max(0, length - dataStart);
                _chunkSize = ChunkSize(data, options);
                Count = (int)Math.Max(1, (data + _chunkSize - 1) / _chunkSize);
            }

            internal int Count { get; }

            internal long Start(int index)
            {
                return _dataStart + (index * _chunkSize);
            }

            internal long End(int index)
            {
                return Math.Min(Start(index) + _chunkSize, _length);
            }

            internal bool TryTakeNext(out int index)
            {
                index = Interlocked.Increment(ref _next);
                return index < Count;
            }

            private static long ChunkSize(long length, FixedWidthReaderOptions options)
            {
                if (options.ChunkSize > 0)
                {
                    return options.ChunkSize;
                }
                if (options.Workers == 1)
                {
                    return Math.Max(1, length);
                }
                return Math.Clamp(length / ((long)options.Workers * ChunksPerWorker), MinChunkBytes, MaxChunkBytes);
            }
        }

        /// <summary>Feeds one partition's bytes to a <see cref="LineBufferState"/> and numbers its lines on demand.</summary>
        private sealed class ChunkReader(IRandomAccessSource source, long origin, int maxLineLength)
        {
            private long _readOffset = origin;
            private long _countedTo;
            private long _breaksCounted;

            internal long Origin { get; } = origin;

            internal LineStatus NextLine(ref LineBufferState lines, out ReadOnlySpan<byte> line)
            {
                try
                {
                    while (true)
                    {
                        var status = lines.TryGetLine(out line);
                        if (status != LineStatus.NeedData)
                        {
                            return status;
                        }
                        lines.Compact();
                        lines.GrowIfFull();
                        int read = source.Read(lines.Buffer.AsSpan(lines.End), _readOffset);
                        _readOffset += read;
                        lines.Advance(read);
                    }
                }
                catch (InvalidDataException)
                {
                    // LineBufferState numbers lines from the partition start; the file numbers them from its own.
                    throw ParseErrors.LineTooLong(LineNumberAt(Origin + lines.Consumed), maxLineLength);
                }
            }

            /// <summary>The 1-based line number of the line starting at <paramref name="offset"/>, as a sequential read counts it.</summary>
            // ponytail: counts line breaks from the start of the file on the first failure in a partition,
            // then from the last count; only failing lines pay for it.
            internal long LineNumberAt(long offset)
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
                try
                {
                    while (_countedTo < offset)
                    {
                        int read = source.Read(buffer.AsSpan(0, (int)Math.Min(buffer.Length, offset - _countedTo)), _countedTo);
                        if (read == 0)
                        {
                            break;
                        }
                        _breaksCounted += buffer.AsSpan(0, read).Count((byte)'\n');
                        _countedTo += read;
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
                return _breaksCounted + 1;
            }
        }
    }
}
