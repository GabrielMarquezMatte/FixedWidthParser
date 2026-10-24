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
    /// from a bounded queue into one accumulator each; the caller merges them in chunk order. A queue
    /// about twice the worker count bounds memory to roughly that many chunks, whatever the source's size.
    /// </summary>
    internal static class ParallelStreamAggregator
    {
        private const int DefaultChunkSize = 256 * 1024;
        private const int LinesPerCancellationCheck = 4096;

        [StructLayout(LayoutKind.Auto)]
        private readonly record struct Chunk(int Index, byte[] Buffer, int Length, long LinesBefore);

        [StructLayout(LayoutKind.Auto)]
        private readonly record struct Outcome<TAccumulator>(int Index, TAccumulator? Accumulator, ExceptionDispatchInfo? Failure);

        /// <summary>Folds <paramref name="source"/>, disposing it afterwards unless <paramref name="leaveOpen"/>.</summary>
        internal static async Task<TAccumulator> RunAsync<TModel, TParser, TAccumulator>(
            TParser parser, Stream source, bool leaveOpen, FixedWidthReaderOptions options, CancellationToken ct)
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
            where TParser : struct, IRecordLineParser<TModel>
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            int workerCount = options.Workers;
            var queue = Channel.CreateBounded<Chunk>(new BoundedChannelOptions(workerCount * 2) { SingleWriter = true });
            var workers = new Task<List<Outcome<TAccumulator>>>[workerCount];
            foreach (ref var worker in workers.AsSpan())
            {
                worker = Task.Run(() => ConsumeAsync<TModel, TParser, TAccumulator>(parser, queue.Reader, options, ct), ct);
            }

            ExceptionDispatchInfo? producerFailure = null;
            try
            {
                await ProduceAsync(source, queue.Writer, options, options.ChunkSize > 0 ? options.ChunkSize : DefaultChunkSize, ct).ConfigureAwait(false);
            }
            // Cancellation too: the workers must finish before the chunks left in the queue are returned.
#pragma warning disable CA1031 // Rethrown by Merge once the workers have finished.
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
                var outcomes = (await Task.WhenAll(workers).ConfigureAwait(false)).SelectMany(o => o).OrderBy(o => o.Index);
                return Merge<TModel, TAccumulator>(outcomes, producerFailure);
            }
            finally
            {
                while (queue.Reader.TryRead(out var abandoned))
                {
                    ArrayPool<byte>.Shared.Return(abandoned.Buffer);
                }
            }
        }

        // Every chunk precedes the point where the producer failed, so a worker's failure comes first in the source.
        private static TAccumulator Merge<TModel, TAccumulator>(IEnumerable<Outcome<TAccumulator>> outcomes, ExceptionDispatchInfo? producerFailure)
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            TAccumulator? merged = default;
            bool first = true;
            foreach (var outcome in outcomes)
            {
                outcome.Failure?.Throw();
                if (first)
                {
                    merged = outcome.Accumulator;
                    first = false;
                    continue;
                }
                merged!.Merge(outcome.Accumulator!);
            }
            producerFailure?.Throw();
            if (first)
            {
                return new TAccumulator();
            }
            return merged!;
        }

        private static async Task ProduceAsync(Stream source, ChannelWriter<Chunk> queue, FixedWidthReaderOptions options, int chunkSize, CancellationToken ct)
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

        private static async Task<List<Outcome<TAccumulator>>> ConsumeAsync<TModel, TParser, TAccumulator>(
            TParser parser, ChannelReader<Chunk> queue, FixedWidthReaderOptions options, CancellationToken ct)
            where TParser : struct, IRecordLineParser<TModel>
            where TAccumulator : IRecordAccumulator<TAccumulator, TModel>, new()
        {
            var outcomes = new List<Outcome<TAccumulator>>();
            var context = options.Context;
            await foreach (var chunk in queue.ReadAllAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    outcomes.Add(new(chunk.Index, Fold<TModel, TParser, TAccumulator>(parser, chunk, options, in context, ct), null));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    outcomes.Add(new(chunk.Index, default, ExceptionDispatchInfo.Capture(ex)));
                }
            }
            return outcomes;
        }

        private static TAccumulator Fold<TModel, TParser, TAccumulator>(
            TParser parser, Chunk chunk, FixedWidthReaderOptions options, in FixedWidthContext context, CancellationToken ct)
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
    }
}
