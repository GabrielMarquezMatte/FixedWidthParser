using System.Runtime.InteropServices;
using FixedWidthParser.Native.Reading;
using FixedWidthParser.Native.Writing;

namespace FixedWidthParser.Native
{
    internal static unsafe partial class Exports
    {
        [UnmanagedCallersOnly(EntryPoint = "fw_write_file")]
        public static int WriteFile(byte* path, int pathLength, NativeTableRaw* table, nint layout, NativeWriteOptionsRaw* options)
        {
            if (path is null || pathLength <= 0 || table is null)
            {
                return InvalidArgument("fw_write_file needs a path and a table.");
            }
            return WithLayout(layout, l => WriteApi.WriteFile(new ReadOnlySpan<byte>(path, pathLength), table, l, options));
        }

        [UnmanagedCallersOnly(EntryPoint = "fw_write_memory")]
        public static int WriteMemory(NativeTableRaw* table, nint layout, NativeWriteOptionsRaw* options, NativeBufferRaw* outBuffer)
        {
            if (table is null || outBuffer is null)
            {
                return InvalidArgument("fw_write_memory needs a table and out_buffer.");
            }
            *outBuffer = default;
            return WithLayout(layout, l => WriteApi.WriteMemory(table, l, options, outBuffer));
        }

        [UnmanagedCallersOnly(EntryPoint = "fw_free_buffer")]
        public static void FreeBuffer(NativeBufferRaw* buffer)
        {
            WriteApi.FreeBuffer(buffer);
        }
    }
}
