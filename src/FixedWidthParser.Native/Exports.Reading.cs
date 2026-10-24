using System.Runtime.InteropServices;
using FixedWidthParser.Native.Layouts;
using FixedWidthParser.Native.Reading;

namespace FixedWidthParser.Native
{
    internal static unsafe partial class Exports
    {
        [UnmanagedCallersOnly(EntryPoint = "fw_read_file")]
        public static int ReadFile(byte* path, int pathLength, nint layout, NativeReadOptionsRaw* options, NativeTableRaw* outTable, NativeReadResultRaw* outResult)
        {
            if (path is null || pathLength <= 0 || outTable is null)
            {
                return InvalidArgument("fw_read_file needs a path and out_table.");
            }
            *outTable = default;
            return WithLayout(layout, l => ReadApi.ReadFile(new ReadOnlySpan<byte>(path, pathLength), l, options, outTable, outResult));
        }

        [UnmanagedCallersOnly(EntryPoint = "fw_read_memory")]
        public static int ReadMemory(byte* data, long dataLength, nint layout, NativeReadOptionsRaw* options, NativeTableRaw* outTable, NativeReadResultRaw* outResult)
        {
            if ((data is null && dataLength != 0) || dataLength < 0 || outTable is null)
            {
                return InvalidArgument("fw_read_memory needs data for a non-zero data_len, and out_table.");
            }
            *outTable = default;
            return WithLayout(layout, l => ReadApi.ReadMemory(data, dataLength, l, options, outTable, outResult));
        }

        [UnmanagedCallersOnly(EntryPoint = "fw_free_table")]
        public static void FreeTable(NativeTableRaw* table)
        {
            NativeTable.Free(table);
        }

        private static int WithLayout(nint handle, Func<NativeLayout, int> call)
        {
            var layout = NativeHandleTable.Resolve<NativeLayout>(handle);
            if (layout is null)
            {
                NativeApi.SetLastError("The layout handle is not a live fw_layout.");
                return NativeStatus.InvalidHandle;
            }
            try
            {
                return call(layout);
            }
            catch (Exception ex)
            {
                NativeApi.SetLastError(ex.Message);
                return NativeStatus.Error;
            }
        }
    }
}
