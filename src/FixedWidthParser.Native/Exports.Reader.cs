using System.Runtime.InteropServices;
using FixedWidthParser.Native.Reading;

namespace FixedWidthParser.Native
{
    internal static unsafe partial class Exports
    {
        [UnmanagedCallersOnly(EntryPoint = "fw_reader_open_file")]
        public static int ReaderOpenFile(byte* path, int pathLength, nint layout, NativeReaderOptionsRaw* options, nint* outReader)
        {
            if (path is null || pathLength <= 0 || outReader is null)
            {
                return NativeApi.InvalidArgument("fw_reader_open_file needs a path and out_reader.");
            }
            *outReader = 0;
            return WithLayout(layout, l =>
            {
                int status = ReaderApi.OpenFile(new ReadOnlySpan<byte>(path, pathLength), l, options, out nint reader);
                *outReader = reader;
                return status;
            });
        }

        [UnmanagedCallersOnly(EntryPoint = "fw_reader_open_memory")]
        public static int ReaderOpenMemory(byte* data, long dataLength, nint layout, NativeReaderOptionsRaw* options, nint* outReader)
        {
            if ((data is null && dataLength != 0) || dataLength < 0 || outReader is null)
            {
                return NativeApi.InvalidArgument("fw_reader_open_memory needs data for a non-zero data_len, and out_reader.");
            }
            *outReader = 0;
            return WithLayout(layout, l =>
            {
                int status = ReaderApi.OpenMemory(data, dataLength, l, options, out nint reader);
                *outReader = reader;
                return status;
            });
        }

        [UnmanagedCallersOnly(EntryPoint = "fw_reader_open_stream")]
        public static int ReaderOpenStream(NativeStreamRaw* stream, nint layout, NativeReaderOptionsRaw* options, nint* outReader)
        {
            if (outReader is null)
            {
                return NativeApi.InvalidArgument("fw_reader_open_stream needs out_reader.");
            }
            *outReader = 0;
#pragma warning disable CA2000 // The reader takes the stream over, or it is disposed below.
            if (!CallbackReadStream.TryCreate(stream, "fw_reader_open_stream", out var source))
            {
                return NativeStatus.InvalidArgument;
            }
            int status = WithLayout(layout, l =>
            {
                int opened = ReaderApi.OpenStream(source, l, options, out nint reader);
                *outReader = reader;
                return opened;
            });
            if (status != NativeStatus.Ok)
            {
                // A layout that is not live never reached OpenStream, which releases the stream on its own failures.
                source.Dispose();
            }
#pragma warning restore CA2000
            return status;
        }

        [UnmanagedCallersOnly(EntryPoint = "fw_set_source_error")]
        public static void SetSourceError(byte* message, int length)
        {
            SourceErrors.Set(message is null || length <= 0 ? null : System.Text.Encoding.UTF8.GetString(message, length));
        }

        [UnmanagedCallersOnly(EntryPoint = "fw_reader_next")]
        public static int ReaderNext(nint reader, NativeTableRaw* outTable)
        {
            if (outTable is null)
            {
                return NativeApi.InvalidArgument("fw_reader_next needs out_table.");
            }
            return ReaderApi.Next(reader, outTable);
        }

        [UnmanagedCallersOnly(EntryPoint = "fw_reader_close")]
        public static void ReaderClose(nint reader)
        {
            try
            {
                ReaderApi.Close(reader);
            }
            catch (Exception ex)
            {
                NativeApi.SetLastError(ex.Message);
            }
        }
    }
}
