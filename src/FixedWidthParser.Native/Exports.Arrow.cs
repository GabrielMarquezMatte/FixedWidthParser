using System.Runtime.InteropServices;
using FixedWidthParser.Native.Arrow;
using FixedWidthParser.Native.Reading;

namespace FixedWidthParser.Native
{
    internal static unsafe partial class Exports
    {
        [UnmanagedCallersOnly(EntryPoint = "fw_read_arrow_file")]
        public static int ReadArrowFile(byte* path, int pathLength, nint layout, NativeReadOptionsRaw* options, ArrowArray* outArray, ArrowSchema* outSchema, NativeReadResultRaw* outResult)
        {
            if (path is null || pathLength <= 0 || outArray is null || outSchema is null)
            {
                return NativeApi.InvalidArgument("fw_read_arrow_file needs a path, out_array and out_schema.");
            }
            *outArray = default;
            *outSchema = default;
            return WithLayout(layout, l => ArrowApi.ReadFile(new ReadOnlySpan<byte>(path, pathLength), l, options, outArray, outSchema, outResult));
        }

        [UnmanagedCallersOnly(EntryPoint = "fw_read_arrow_memory")]
        public static int ReadArrowMemory(byte* data, long dataLength, nint layout, NativeReadOptionsRaw* options, ArrowArray* outArray, ArrowSchema* outSchema, NativeReadResultRaw* outResult)
        {
            if ((data is null && dataLength != 0) || dataLength < 0 || outArray is null || outSchema is null)
            {
                return NativeApi.InvalidArgument("fw_read_arrow_memory needs data for a non-zero data_len, out_array and out_schema.");
            }
            *outArray = default;
            *outSchema = default;
            return WithLayout(layout, l => ArrowApi.ReadMemory(data, dataLength, l, options, outArray, outSchema, outResult));
        }

        [UnmanagedCallersOnly(EntryPoint = "fw_read_arrow_stream_file")]
        public static int ReadArrowStreamFile(byte* path, int pathLength, nint layout, NativeReaderOptionsRaw* options, ArrowArrayStream* outStream)
        {
            if (path is null || pathLength <= 0 || outStream is null)
            {
                return NativeApi.InvalidArgument("fw_read_arrow_stream_file needs a path and out_stream.");
            }
            *outStream = default;
            return WithLayout(layout, l => ArrowApi.OpenStreamFile(new ReadOnlySpan<byte>(path, pathLength), l, options, outStream));
        }

        [UnmanagedCallersOnly(EntryPoint = "fw_read_arrow_stream_memory")]
        public static int ReadArrowStreamMemory(byte* data, long dataLength, nint layout, NativeReaderOptionsRaw* options, ArrowArrayStream* outStream)
        {
            if ((data is null && dataLength != 0) || dataLength < 0 || outStream is null)
            {
                return NativeApi.InvalidArgument("fw_read_arrow_stream_memory needs data for a non-zero data_len, and out_stream.");
            }
            *outStream = default;
            return WithLayout(layout, l => ArrowApi.OpenStreamMemory(data, dataLength, l, options, outStream));
        }

        [UnmanagedCallersOnly(EntryPoint = "fw_read_arrow_stream_stream")]
        public static int ReadArrowStreamStream(NativeStreamRaw* stream, nint layout, NativeReaderOptionsRaw* options, ArrowArrayStream* outStream)
        {
            if (outStream is null)
            {
                return NativeApi.InvalidArgument("fw_read_arrow_stream_stream needs out_stream.");
            }
            *outStream = default;
#pragma warning disable CA2000 // The reader takes the stream over, or it is disposed below.
            if (!CallbackReadStream.TryCreate(stream, "fw_read_arrow_stream_stream", out var source))
            {
                return NativeStatus.InvalidArgument;
            }
            int status = WithLayout(layout, l => ArrowApi.OpenStreamStream(source, l, options, outStream));
            if (status != NativeStatus.Ok)
            {
                // A layout that is not live never reached OpenStreamStream, which releases the stream on its own failures.
                source.Dispose();
            }
#pragma warning restore CA2000
            return status;
        }

        [UnmanagedCallersOnly]
        internal static void ReleaseArrowSchemaCallback(ArrowSchema* schema)
        {
            ArrowApi.ReleaseArrowSchema((IntPtr)schema);
        }

        [UnmanagedCallersOnly]
        internal static void ReleaseArrowArrayCallback(ArrowArray* array)
        {
            ArrowApi.ReleaseArrowArray((IntPtr)array);
        }

        [UnmanagedCallersOnly]
        internal static int ArrowStreamGetSchema(ArrowArrayStream* stream, ArrowSchema* outSchema)
        {
            return ArrowApi.StreamGetSchema(stream, outSchema);
        }

        [UnmanagedCallersOnly]
        internal static int ArrowStreamGetNext(ArrowArrayStream* stream, ArrowArray* outArray)
        {
            return ArrowApi.StreamGetNext(stream, outArray);
        }

        [UnmanagedCallersOnly]
        internal static IntPtr ArrowStreamGetLastError(ArrowArrayStream* stream)
        {
            return ArrowApi.StreamGetLastError(stream);
        }

        [UnmanagedCallersOnly]
        internal static void ArrowStreamRelease(ArrowArrayStream* stream)
        {
            ArrowApi.StreamRelease(stream);
        }
    }
}
