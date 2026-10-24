using System.Buffers;
using System.Runtime.ExceptionServices;
using FixedWidthParser.Infrastructure;
using Microsoft.Win32.SafeHandles;

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
            TParser parser, string path, FixedWidthReaderOptions options, FixedWidthParallelOptions parallel, CancellationToken ct)
            where TParser : struct, IRecordLineParser<TModel>
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var plan = new ChunkPlan(RandomAccess.GetLength(handle), parallel);
            var results = new TAccumulator[plan.Count];
            var failures = new ExceptionDispatchInfo?[plan.Count];
            var workers = new Task[Math.Min(parallel.Workers, plan.Count)];
            foreach (ref var worker in workers.AsSpan())
            {
                worker = Task.Run(() => Drain<TModel, TParser, TAccumulator>(parser, handle, plan, options, results, failures, ct), ct);
            }
            await Task.WhenAll(workers).ConfigureAwait(false);

            failures[0]?.Throw();
            var merged = results[0];
            for (int i = 1; i < plan.Count; i++)
            {
                failures[i]?.Throw();
                merged.Merge(results[i]);
            }
            return merged;
        }

        private static void Drain<TModel, TParser, TAccumulator>(
            TParser parser, SafeFileHandle handle, ChunkPlan plan, FixedWidthReaderOptions options,
            TAccumulator[] results, ExceptionDispatchInfo?[] failures, CancellationToken ct)
            where TParser : struct, IRecordLineParser<TModel>
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            while (plan.TryTakeNext(out int index))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    results[index] = AggregateChunk<TModel, TParser, TAccumulator>(parser, handle, plan.Start(index), plan.End(index), options, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failures[index] = ExceptionDispatchInfo.Capture(ex);
                }
            }
        }

        private static TAccumulator AggregateChunk<TModel, TParser, TAccumulator>(
            TParser parser, SafeFileHandle handle, long start, long end, FixedWidthReaderOptions options, CancellationToken ct)
            where TParser : struct, IRecordLineParser<TModel>
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            var accumulator = new TAccumulator();
            var context = options.Context;
            var reader = new ChunkReader(handle, start == 0 ? 0 : start - 1, options.MaxLineLength);
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

        /// <summary>Parses one line and folds it in; <see langword="false"/> when it does not parse.</summary>
        internal static bool TryAdd<TModel, TParser, TAccumulator>(TParser parser, ReadOnlySpan<byte> line, in FixedWidthContext context, ref TAccumulator accumulator)
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
            private readonly long _length;
            private readonly long _chunkSize;
            private int _next = -1;

            internal ChunkPlan(long length, FixedWidthParallelOptions parallel)
            {
                _length = length;
                _chunkSize = ChunkSize(length, parallel);
                Count = (int)Math.Max(1, (length + _chunkSize - 1) / _chunkSize);
            }

            internal int Count { get; }

            internal long Start(int index)
            {
                return index * _chunkSize;
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

            private static long ChunkSize(long length, FixedWidthParallelOptions parallel)
            {
                if (parallel.ChunkSize > 0)
                {
                    return parallel.ChunkSize;
                }
                if (parallel.Workers == 1)
                {
                    return Math.Max(1, length);
                }
                return Math.Clamp(length / ((long)parallel.Workers * ChunksPerWorker), MinChunkBytes, MaxChunkBytes);
            }
        }

        /// <summary>Feeds one partition's bytes to a <see cref="LineBufferState"/> and numbers its lines on demand.</summary>
        private sealed class ChunkReader(SafeFileHandle handle, long origin, int maxLineLength)
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
                        int read = RandomAccess.Read(handle, lines.Buffer.AsSpan(lines.End), _readOffset);
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
                        int read = RandomAccess.Read(handle, buffer.AsSpan(0, (int)Math.Min(buffer.Length, offset - _countedTo)), _countedTo);
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
