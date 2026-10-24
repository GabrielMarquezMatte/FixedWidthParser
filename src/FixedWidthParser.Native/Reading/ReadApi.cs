using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using FixedWidthParser.Native.Layouts;
using FixedWidthParser.Readers;

namespace FixedWidthParser.Native.Reading
{
    /// <summary><c>fw_read_file</c> and <c>fw_read_memory</c>: a whole source into one columnar table.</summary>
    internal static unsafe class ReadApi
    {
        internal static int ReadFile(ReadOnlySpan<byte> path, NativeLayout layout, NativeReadOptionsRaw* options, NativeTableRaw* outTable, NativeReadResultRaw* outResult, int chunkSize = 0)
        {
            try
            {
                using var handle = File.OpenHandle(Encoding.UTF8.GetString(path), FileMode.Open, FileAccess.Read, FileShare.Read);
                return Read(new FileSource(handle), layout, options, outTable, outResult, chunkSize);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                NativeApi.SetLastError(ex.Message);
                return NativeStatus.Error;
            }
        }

        // chunkSize: bytes per partition, 0 sizes them from the source; tests set it to cut every boundary.
        internal static int ReadMemory(byte* data, long length, NativeLayout layout, NativeReadOptionsRaw* options, NativeTableRaw* outTable, NativeReadResultRaw* outResult, int chunkSize = 0)
        {
            return Read(new PointerSource(data, length), layout, options, outTable, outResult, chunkSize);
        }

        private static int Read(IRandomAccessSource source, NativeLayout layout, NativeReadOptionsRaw* options, NativeTableRaw* outTable, NativeReadResultRaw* outResult, int chunkSize)
        {
            *outTable = default;
            int status = CheckResult(outResult);
            if (status != NativeStatus.Ok)
            {
                return status;
            }
            status = ReadSettings.TryCreate(options, out var settings);
            if (status != NativeStatus.Ok)
            {
                return status;
            }
            try
            {
                long dataStart = HeaderEnd(source, settings!.HeaderLines, out long headerLines);
                var skipped = new StrongBox<long>(headerLines);
                var readerOptions = settings.ReaderOptions(skipped, settings.DegreeOfParallelism, chunkSize);
#pragma warning disable VSTHRD002 // An export is synchronous and runs with no synchronization context.
                using var table = ParallelAggregator.RunAsync(source, dataStart, () => new TableSink(layout), readerOptions, CancellationToken.None)
                    .GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
                table.Publish(outTable);
                WriteResult(outResult, table.TotalRows, skipped.Value);
                return NativeStatus.Ok;
            }
            catch (FixedWidthParseException ex)
            {
                NativeApi.SetParseError(ex, layout.IndexOf(ex.ColumnName));
                return NativeStatus.ParseError;
            }
            catch (Exception ex)
            {
                NativeApi.SetLastError(ex.Message);
                return NativeStatus.Error;
            }
        }

        private static int CheckResult(NativeReadResultRaw* result)
        {
            if (result is not null && result->StructSize != sizeof(NativeReadResultRaw))
            {
                NativeApi.SetLastError($"fw_read_result struct_size is {result->StructSize}; expected {sizeof(NativeReadResultRaw)}.");
                return NativeStatus.InvalidArgument;
            }
            return NativeStatus.Ok;
        }

        private static void WriteResult(NativeReadResultRaw* result, long rows, long skipped)
        {
            if (result is null)
            {
                return;
            }
            result->LinesRead = rows;
            result->LinesSkipped = skipped;
        }

        /// <summary>The offset just past the first <paramref name="count"/> physical lines, and how many lines that was.</summary>
        internal static long HeaderEnd(IRandomAccessSource source, long count, out long skipped)
        {
            skipped = 0;
            if (count == 0)
            {
                return 0;
            }
            byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            try
            {
                long offset = 0;
                int read;
                while ((read = source.Read(buffer, offset)) > 0)
                {
                    var block = buffer.AsSpan(0, read);
                    for (int at = block.IndexOf((byte)'\n'); at >= 0; at = NextBreak(block, at))
                    {
                        if (++skipped == count)
                        {
                            return offset + at + 1;
                        }
                    }
                    offset += read;
                }
                skipped += offset > 0 && LastByte(source, offset) != (byte)'\n' ? 1 : 0;
                return offset;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static int NextBreak(ReadOnlySpan<byte> block, int at)
        {
            int next = block[(at + 1)..].IndexOf((byte)'\n');
            return next < 0 ? -1 : at + 1 + next;
        }

        private static byte LastByte(IRandomAccessSource source, long length)
        {
            Span<byte> last = stackalloc byte[1];
            source.Read(last, length - 1);
            return last[0];
        }
    }
}
