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

        /// <inheritdoc cref="CreateStream"/>
        internal static int OpenStream(Stream source, NativeLayout layout, NativeReaderOptionsRaw* options, out nint reader)
        {
            int status = CreateStream(source, layout, options, out var created);
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
                stream.Position = dataStart;
                reader = new NativeTableReader(stream, dataStart == 0, headerLines, layout, settings, batchRows);
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
            var stream = new UnmanagedMemoryStream(data, length) { Position = dataStart };
#pragma warning restore CA2000
            reader = new NativeTableReader(stream, dataStart == 0, headerLines, layout, settings, batchRows);
            return NativeStatus.Ok;
        }

        /// <summary>
        /// Reads <paramref name="source"/> forward from where it is, skipping the header lines as it goes. Takes ownership:
        /// <paramref name="source"/> is disposed, so its <c>release</c> runs, if this fails, or with the reader.
        /// </summary>
        internal static int CreateStream(Stream source, NativeLayout layout, NativeReaderOptionsRaw* options, out NativeTableReader? reader)
        {
            reader = null;
            int status = TrySettings(options, out var settings, out long batchRows);
            if (status != NativeStatus.Ok)
            {
#pragma warning disable IDISP007 // CreateStream owns the source it is handed.
                source.Dispose();
#pragma warning restore IDISP007
                return status;
            }
            // Buffered so the header skip reads a byte at a time from memory, not from the caller's callback.
#pragma warning disable CA2000 // The reader owns and disposes the stream, and with it the source.
            var stream = new BufferedStream(source, StreamBufferSize);
#pragma warning restore CA2000
            try
            {
                long headerLines = SkipLines(stream, settings!.HeaderLines);
                reader = new NativeTableReader(stream, settings.HeaderLines == 0, headerLines, layout, settings, batchRows);
                return NativeStatus.Ok;
            }
            catch (IOException ex)
            {
                stream.Dispose();
                NativeApi.SetLastError(ex.Message);
                return NativeStatus.Error;
            }
        }

        private const int StreamBufferSize = 64 * 1024;

        /// <summary>Skips <paramref name="count"/> physical lines, as <see cref="ReadApi.HeaderEnd"/> counts them, and returns how many there were.</summary>
        private static long SkipLines(Stream stream, long count)
        {
            long skipped = 0;
            bool partial = false;
            while (skipped < count)
            {
                int b = stream.ReadByte();
                if (b < 0)
                {
                    return skipped + (partial ? 1 : 0);
                }
                partial = b != '\n';
                skipped += partial ? 0 : 1;
            }
            return skipped;
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
            if (raw.BatchRows is < 0 or > int.MaxValue || (raw.Read.StructSize != 0 && raw.Read.StructSize != sizeof(NativeReadOptionsRaw)))
            {
                return NativeApi.InvalidArgument("fw_reader_options: batch_rows must be 0..2147483647, and read.struct_size must be 0 or sizeof(fw_read_options).");
            }
            batchRows = raw.BatchRows;
            var read = raw.Read with { StructSize = sizeof(NativeReadOptionsRaw) };
            return ReadSettings.TryCreate(&read, out settings);
        }
    }
}
