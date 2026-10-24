using System.Text;
using FixedWidthParser.Native.Layouts;
using FixedWidthParser.Readers;

namespace FixedWidthParser.Native.Reading
{
    /// <summary><c>fw_reader_*</c>: batches of a sequential read, behind a handle.</summary>
    internal static unsafe class ReaderApi
    {
#pragma warning disable CA2000, IDISP001 // The handle table owns a reader until fw_reader_close.
        internal static int OpenFile(ReadOnlySpan<byte> path, NativeLayout layout, NativeReaderOptionsRaw* options, out nint reader)
        {
            int status = CreateFile(path, layout, options, out var created);
            reader = status == NativeStatus.Ok ? NativeHandleTable.Register(created!) : 0;
            return status;
        }

        /// <summary>Reads <paramref name="data"/> in place: the caller keeps it alive until the reader is closed.</summary>
        internal static int OpenMemory(byte* data, long length, NativeLayout layout, NativeReaderOptionsRaw* options, out nint reader)
        {
            int status = CreateMemory(data, length, layout, options, out var created);
            reader = status == NativeStatus.Ok ? NativeHandleTable.Register(created!) : 0;
            return status;
        }

#pragma warning restore CA2000, IDISP001

        internal static int CreateFile(ReadOnlySpan<byte> path, NativeLayout layout, NativeReaderOptionsRaw* options, out NativeTableReader? reader)
        {
            reader = null;
            int status = TrySettings(options, out var settings, out long batchRows);
            if (status != NativeStatus.Ok)
            {
                return status;
            }
            FileStream? stream = null;
            try
            {
                stream = new FileStream(Encoding.UTF8.GetString(path), FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, FileOptions.SequentialScan);
                long dataStart = ReadApi.HeaderEnd(new FileSource(stream.SafeFileHandle), settings!.HeaderLines, out long headerLines);
                reader = new NativeTableReader(stream, dataStart, headerLines, layout, settings, batchRows);
                return NativeStatus.Ok;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                stream?.Dispose();
                NativeApi.SetLastError(ex.Message);
                return NativeStatus.Error;
            }
        }

        internal static int CreateMemory(byte* data, long length, NativeLayout layout, NativeReaderOptionsRaw* options, out NativeTableReader? reader)
        {
            reader = null;
            int status = TrySettings(options, out var settings, out long batchRows);
            if (status != NativeStatus.Ok)
            {
                return status;
            }
            long dataStart = ReadApi.HeaderEnd(new PointerSource(data, length), settings!.HeaderLines, out long headerLines);
#pragma warning disable CA2000 // The reader owns and disposes the stream.
            var stream = new UnmanagedMemoryStream(data, length);
#pragma warning restore CA2000
            reader = new NativeTableReader(stream, dataStart, headerLines, layout, settings, batchRows);
            return NativeStatus.Ok;
        }

        internal static int Next(nint handle, NativeTableRaw* table)
        {
            *table = default;
            var reader = NativeHandleTable.Resolve<NativeTableReader>(handle);
            if (reader is null)
            {
                NativeApi.SetLastError("The reader handle is not a live fw_reader.");
                return NativeStatus.InvalidHandle;
            }
            return NextBatch(reader, table);
        }

        /// <summary>The next batch of <paramref name="reader"/>, with every failure turned into a status and the last error.</summary>
        internal static int NextBatch(NativeTableReader reader, NativeTableRaw* table)
        {
            try
            {
                return reader.Next(table);
            }
            catch (FixedWidthParseException ex)
            {
                NativeApi.SetParseError(ex, reader.Layout.IndexOf(ex.ColumnName));
                return NativeStatus.ParseError;
            }
            catch (Exception ex)
            {
                NativeApi.SetLastError(ex.Message);
                return NativeStatus.Error;
            }
        }

        internal static void Close(nint handle)
        {
            if (NativeHandleTable.TryUnregister<NativeTableReader>(handle, out var reader))
            {
                reader!.Dispose();
            }
        }

        private static int TrySettings(NativeReaderOptionsRaw* options, out ReadSettings? settings, out long batchRows)
        {
            settings = null;
            batchRows = 0;
            int status = NativeOptions.TryRead(options, "fw_reader_options", out var raw);
            if (status != NativeStatus.Ok)
            {
                return status;
            }
            if (raw.BatchRows < 0 || (raw.Read.StructSize != 0 && raw.Read.StructSize != sizeof(NativeReadOptionsRaw)))
            {
                NativeApi.SetLastError("fw_reader_options: batch_rows must not be negative, and read.struct_size must be 0 or sizeof(fw_read_options).");
                return NativeStatus.InvalidArgument;
            }
            batchRows = raw.BatchRows;
            var read = raw.Read with { StructSize = sizeof(NativeReadOptionsRaw) };
            return ReadSettings.TryCreate(&read, out settings);
        }
    }
}
