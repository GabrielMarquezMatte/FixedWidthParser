using System.Buffers;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using FixedWidthParser.Infrastructure;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Parallel aggregation over a sequential source. One producer reads the source into pooled chunks,
    /// cuts each after its last line break, carries the rest into the next chunk, and counts the line
    /// breaks it hands over, so every chunk knows the number of its first line. Workers fold the chunks
    /// from a bounded queue into one accumulator each, merged in chunk order as they complete. Every chunk
    /// holds one of a few slots per worker from the moment it is produced until it is merged, which bounds
    /// memory, chunks and unmerged accumulators alike, whatever the source's size. The first failure in source
    /// order ends the read: no later chunk can change the result, so the producer stops reading and the workers
    /// stop taking chunks, instead of waiting for the end of a source that may never come.
    /// </summary>
    internal static class ParallelStreamAggregator
    {
        private const int DefaultChunkSize = 256 * 1024;
        private const int LinesPerCancellationCheck = 4096;
        private const int SlotsPerWorker = 4;

        [StructLayout(LayoutKind.Auto)]
        private readonly record struct Chunk(int Index, byte[] Buffer, int Length, long LinesBefore);

        [StructLayout(LayoutKind.Auto)]
        private readonly record struct Outcome<TAccumulator>(int Index, TAccumulator? Accumulator, ExceptionDispatchInfo? Failure);

        /// <summary>Folds <paramref name="source"/>, disposing it afterwards unless <paramref name="leaveOpen"/>.</summary>
        internal static async Task<TAccumulator> RunAsync<TModel, TParser, TAccumulator>(
            TParser parser, Stream source, bool leaveOpen, FixedWidthReaderOptions options, CancellationToken ct)
            where TModel : allows ref struct
            where TParser : struct, IRecordLineParser<TModel>
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            try
            {
                if (options.Workers == 1)
                {
                    return await SequentialAsync<TModel, TParser, TAccumulator>(parser, source, options, ct).ConfigureAwait(false);
                }
                return await PartitionedAsync<TModel, TParser, TAccumulator>(parser, source, options, ct).ConfigureAwait(false);
            }
            finally
            {
                if (!leaveOpen)
                {
                    await source.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        private static async Task<TAccumulator> SequentialAsync<TModel, TParser, TAccumulator>(
            TParser parser, Stream source, FixedWidthReaderOptions options, CancellationToken ct)
            where TModel : allows ref struct
            where TParser : struct, IRecordLineParser<TModel>
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            var accumulator = new TAccumulator();
            var records = new FixedWidthAsyncRecordEnumerable<TModel, TParser>(parser, source, ownsStream: false, options);
            await foreach (var model in records.WithCancellation(ct).ConfigureAwait(false))
            {
                accumulator.Add(in model);
            }
            return accumulator;
        }

        private static async Task<TAccumulator> PartitionedAsync<TModel, TParser, TAccumulator>(
            TParser parser, Stream source, FixedWidthReaderOptions options, CancellationToken ct)
            where TModel : allows ref struct
            where TParser : struct, IRecordLineParser<TModel>
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            int workerCount = options.Workers;
            // Two chunks queued and one folding per worker leave one slot per worker for results waiting on an earlier chunk.
            using var slots = new SemaphoreSlim(workerCount * SlotsPerWorker);
            // Cancelled by the caller, or by the merge once a failure decides the result.
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var queue = Channel.CreateBounded<Chunk>(new BoundedChannelOptions(workerCount * 2) { SingleWriter = true });
            var merge = new OrderedMerge<TModel, TAccumulator>(slots, stop);
            var workers = new Task[workerCount];
            foreach (ref var worker in workers.AsSpan())
            {
#pragma warning disable IDISP013 // Every worker is awaited below, before the slots are disposed.
                worker = Task.Run(() => ConsumeAsync<TModel, TParser, TAccumulator>(parser, queue.Reader, merge, options, stop.Token, ct), ct);
#pragma warning restore IDISP013
            }

            ExceptionDispatchInfo? producerFailure = null;
            try
            {
                await ProduceAsync(source, queue.Writer, slots, options, options.ChunkSize > 0 ? options.ChunkSize : DefaultChunkSize, stop.Token).ConfigureAwait(false);
            }
            // Cancellation too: the workers must finish before the chunks left in the queue are returned.
#pragma warning disable CA1031 // Rethrown by merge.Result once the workers have finished.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                producerFailure = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                queue.Writer.TryComplete();
            }

            try
            {
                try
                {
                    await Task.WhenAll(workers).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested && merge.Failed)
                {
                    // Workers stopped by a failure, not by the caller: the failure is what Result throws.
                }
                return merge.Result(producerFailure);
            }
            finally
            {
                while (queue.Reader.TryRead(out var abandoned))
                {
                    ArrayPool<byte>.Shared.Return(abandoned.Buffer);
                }
            }
        }

        private static async Task ProduceAsync(
            Stream source, ChannelWriter<Chunk> queue, SemaphoreSlim slots, FixedWidthReaderOptions options, int chunkSize, CancellationToken ct)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
            int filled = 0;
            int index = 0;
            long linesBefore = 0;
            try
            {
                while (true)
                {
                    if (filled == buffer.Length)
                    {
                        buffer = GrowUnterminated(buffer, linesBefore, options.MaxLineLength);
                    }
                    int read = await source.ReadAsync(buffer.AsMemory(filled), ct).ConfigureAwait(false);
                    filled += read;
                    bool eof = read == 0;
                    if (eof && filled == 0)
                    {
                        return;
                    }
                    if (!eof && filled < buffer.Length)
                    {
                        continue;
                    }

                    int cut = eof ? filled : buffer.AsSpan(0, filled).LastIndexOf((byte)'\n') + 1;
                    if (cut == 0)
                    {
                        continue;
                    }
                    int tail = filled - cut;
                    byte[] next = ArrayPool<byte>.Shared.Rent(Math.Max(chunkSize, tail * 2));
                    buffer.AsSpan(cut, tail).CopyTo(next);
                    long lines = buffer.AsSpan(0, cut).Count((byte)'\n');
                    try
                    {
                        await slots.WaitAsync(ct).ConfigureAwait(false);
                        await queue.WriteAsync(new Chunk(index++, buffer, cut, linesBefore), ct).ConfigureAwait(false);
                    }
                    catch
                    {
                        ArrayPool<byte>.Shared.Return(next);
                        throw;
                    }
                    buffer = next;
                    filled = tail;
                    linesBefore += lines;
                    if (eof)
                    {
                        return;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        // A full chunk with no line break holds the start of one line; grow it as LineBufferState does.
        private static byte[] GrowUnterminated(byte[] buffer, long linesBefore, int maxLineLength)
        {
            if (buffer.Length > maxLineLength + 1L)
            {
                throw ParseErrors.LineTooLong(linesBefore + 1, maxLineLength);
            }
            byte[] bigger = ArrayPool<byte>.Shared.Rent((int)Math.Min(buffer.Length * 2L, maxLineLength + 2L));
            buffer.CopyTo(bigger, 0);
            ArrayPool<byte>.Shared.Return(buffer);
            return bigger;
        }

        // stop is the caller's token, cancelled too once a failure decides the result; ct is the caller's alone.
        private static async Task ConsumeAsync<TModel, TParser, TAccumulator>(
            TParser parser, ChannelReader<Chunk> queue, OrderedMerge<TModel, TAccumulator> merge, FixedWidthReaderOptions options,
            CancellationToken stop, CancellationToken ct)
            where TModel : allows ref struct
            where TParser : struct, IRecordLineParser<TModel>
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            var context = options.Context;
            await foreach (var chunk in queue.ReadAllAsync(stop).ConfigureAwait(false))
            {
                Outcome<TAccumulator> outcome;
                try
                {
                    outcome = new(chunk.Index, Fold<TModel, TParser, TAccumulator>(parser, chunk, options, in context, stop), null);
                }
                // Anything but the caller's cancellation, which stops everything, becomes this chunk's result: a chunk
                // that never completes would hold its slot, and the producer would wait for it forever.
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    outcome = new(chunk.Index, default, ExceptionDispatchInfo.Capture(ex));
                }
                merge.Complete(outcome);
            }
        }

        private static TAccumulator Fold<TModel, TParser, TAccumulator>(
            TParser parser, Chunk chunk, FixedWidthReaderOptions options, in FixedWidthContext context, CancellationToken ct)
            where TModel : allows ref struct
            where TParser : struct, IRecordLineParser<TModel>
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            var accumulator = new TAccumulator();
            var lines = default(LineBufferState);
            lines.Wrap(chunk.Buffer, chunk.Length, chunk.LinesBefore, options, chunk.Index == 0 && options.SkipsByteOrderMark);
            long delivered = 0;
            try
            {
                while (lines.TryGetLine(out var line) == LineStatus.Line)
                {
                    if (++delivered % LinesPerCancellationCheck == 0)
                    {
                        ct.ThrowIfCancellationRequested();
                    }
                    if (!ParallelAggregator.TryAdd<TModel, TParser, TAccumulator>(parser, line, in context, ref accumulator))
                    {
                        ParseErrors.HandleFailure<TModel, TParser>(parser, line, lines.LineNumber, in context, options.OnParseError);
                    }
                }
            }
            finally
            {
                lines.Return();
            }
            return accumulator;
        }

        /// <summary>
        /// Merges the workers' accumulators left to right as soon as every earlier chunk has been merged, and
        /// frees each chunk's slot then: a slow early chunk stalls the producer instead of letting later
        /// results pile up.
        /// </summary>
        private sealed class OrderedMerge<TModel, TAccumulator>(SemaphoreSlim slots, CancellationTokenSource stop)
            where TModel : allows ref struct
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            private readonly Lock _gate = new();
            private readonly Dictionary<int, Outcome<TAccumulator>> _pending = [];
            private int _next;
            private bool _any;
            private TAccumulator? _merged;
            private ExceptionDispatchInfo? _failure;

            /// <summary>True once a failure in source order decides the result.</summary>
            public bool Failed => Volatile.Read(ref _failure) is not null;

            public void Complete(Outcome<TAccumulator> outcome)
            {
                int released = 0;
                bool failedNow;
                lock (_gate)
                {
                    bool failedBefore = _failure is not null;
                    _pending.Add(outcome.Index, outcome);
                    while (_pending.Remove(_next, out var ready))
                    {
                        _next++;
                        released++;
                        // Past the first failure the results are dropped; only their slots still matter.
                        _failure ??= ready.Failure;
                        if (_failure is null)
                        {
                            Accept(ready.Accumulator!);
                        }
                    }
                    failedNow = !failedBefore && _failure is not null;
                }
                // Outside the lock: cancelling runs the producer's and workers' callbacks.
                if (failedNow)
                {
                    stop.Cancel();
                }
                if (released > 0)
                {
                    slots.Release(released);
                }
            }

            // Every chunk precedes the point where the producer failed, so a worker's failure comes first in the source.
            public TAccumulator Result(ExceptionDispatchInfo? producerFailure)
            {
                _failure?.Throw();
                producerFailure?.Throw();
                return _any ? _merged! : new TAccumulator();
            }

            private void Accept(TAccumulator accumulator)
            {
                if (!_any)
                {
                    _merged = accumulator;
                    _any = true;
                    return;
                }
                try
                {
                    _merged!.Merge(accumulator);
                }
                // Thrown by Result instead, so the slots keep flowing and the producer is never left waiting.
#pragma warning disable CA1031
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    _failure = ExceptionDispatchInfo.Capture(ex);
                }
            }
        }
    }
}
