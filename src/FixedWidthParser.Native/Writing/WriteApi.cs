using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using FixedWidthParser.Native.Layouts;
using FixedWidthParser.Native.Reading;

namespace FixedWidthParser.Native.Writing
{
    /// <summary>
    /// <c>fw_write_file</c> and <c>fw_write_memory</c>. Every line has the same length, so the output size and
    /// each batch's offset are known up front: batches format in parallel straight into place, and the bytes
    /// equal a serial write.
    /// </summary>
    internal static unsafe class WriteApi
    {
        internal static int WriteMemory(NativeTableRaw* table, NativeLayout layout, NativeWriteOptionsRaw* options, NativeBufferRaw* outBuffer)
        {
            *outBuffer = default;
            int status = Prepare(table, layout, options, out var formatter, out var settings, out long lineBytes);
            if (status != NativeStatus.Ok)
            {
                return status;
            }
            long total = formatter!.Rows * lineBytes;
            var data = (byte*)Marshal.AllocHGlobal((nint)Math.Max(1, total));
            status = Run(formatter, settings!, lineBytes, (first, count) =>
            {
                FormatBatch(formatter, settings!, layout, first, new Span<byte>(data + (first * lineBytes), (int)(count * lineBytes)));
            });
            if (status != NativeStatus.Ok)
            {
                Marshal.FreeHGlobal((IntPtr)data);
                return status;
            }
            outBuffer->Data = data;
            outBuffer->Length = total;
            return NativeStatus.Ok;
        }

        internal static int WriteFile(ReadOnlySpan<byte> path, NativeTableRaw* table, NativeLayout layout, NativeWriteOptionsRaw* options)
        {
            int status = Prepare(table, layout, options, out var formatter, out var settings, out long lineBytes);
            if (status != NativeStatus.Ok)
            {
                return status;
            }
            try
            {
                long total = formatter!.Rows * lineBytes;
                using var handle = File.OpenHandle(Encoding.UTF8.GetString(path), FileMode.Create, FileAccess.Write, FileShare.None, FileOptions.None, total);
                RandomAccess.SetLength(handle, total);
                return Run(formatter, settings!, lineBytes, (first, count) => WriteBatch(handle, formatter, settings!, layout, first, count, lineBytes));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                NativeApi.SetLastError(ex.Message);
                return NativeStatus.Error;
            }
        }

        internal static void FreeBuffer(NativeBufferRaw* buffer)
        {
            if (buffer is null)
            {
                return;
            }
            Marshal.FreeHGlobal((IntPtr)buffer->Data);
            *buffer = default;
        }

        private static int Prepare(NativeTableRaw* table, NativeLayout layout, NativeWriteOptionsRaw* options,
            out TableFormatter? formatter, out WriteSettings? settings, out long lineBytes)
        {
            formatter = null;
            lineBytes = 0;
            int status = WriteSettings.TryCreate(options, out settings);
            if (status != NativeStatus.Ok)
            {
                return status;
            }
            lineBytes = layout.LineLength + settings!.NewLine.Length;
            return TableFormatter.TryCreate(table, layout, settings.Encoding, out formatter);
        }

        private static void FormatBatch(TableFormatter formatter, WriteSettings settings, NativeLayout layout, long first, Span<byte> destination)
        {
            int lineLength = layout.LineLength;
            int lineBytes = lineLength + settings.NewLine.Length;
            for (int at = 0; at < destination.Length; at += lineBytes)
            {
                formatter.FormatRow(first + (at / lineBytes), destination.Slice(at, lineLength));
                settings.NewLine.CopyTo(destination.Slice(at + lineLength));
            }
        }

        private static void WriteBatch(Microsoft.Win32.SafeHandles.SafeFileHandle handle, TableFormatter formatter, WriteSettings settings, NativeLayout layout,
            long first, long count, long lineBytes)
        {
            int length = (int)(count * lineBytes);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                FormatBatch(formatter, settings, layout, first, buffer.AsSpan(0, length));
                RandomAccess.Write(handle, buffer.AsSpan(0, length), first * lineBytes);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>Runs <paramref name="batch"/> over every batch of rows, in parallel, and turns a failure into a status.</summary>
        private static int Run(TableFormatter formatter, WriteSettings settings, long lineBytes, Action<long, long> batch)
        {
            long rows = formatter.Rows;
            long batchRows = Math.Max(1, Math.Min(settings.BatchRows, int.MaxValue / lineBytes));
            long batches = (rows + batchRows - 1) / batchRows;
            try
            {
                if (settings.DegreeOfParallelism == 1)
                {
                    for (long b = 0; b < batches; b++)
                    {
                        batch(b * batchRows, Math.Min(batchRows, rows - (b * batchRows)));
                    }
                    return NativeStatus.Ok;
                }
                var parallel = new ParallelOptions { MaxDegreeOfParallelism = settings.DegreeOfParallelism };
                Parallel.For(0, batches, parallel, b => batch(b * batchRows, Math.Min(batchRows, rows - (b * batchRows))));
                return NativeStatus.Ok;
            }
            catch (AggregateException ex)
            {
                NativeApi.SetLastError(ex.InnerExceptions[0].Message);
                return NativeStatus.Error;
            }
            catch (Exception ex)
            {
                NativeApi.SetLastError(ex.Message);
                return NativeStatus.Error;
            }
        }
    }
}
