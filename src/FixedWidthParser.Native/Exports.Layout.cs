using System.Runtime.InteropServices;
using FixedWidthParser.Native.Layouts;

namespace FixedWidthParser.Native
{
    internal static unsafe partial class Exports
    {
        [UnmanagedCallersOnly(EntryPoint = "fw_layout_create")]
        public static int LayoutCreate(NativeColumnSpecRaw* columns, int columnCount, NativeLayoutOptionsRaw* options, nint* outLayout)
        {
            if (outLayout is null || columns is null || columnCount <= 0)
            {
                return InvalidArgument("fw_layout_create needs columns, a positive column_count and out_layout.");
            }
            *outLayout = 0;
            if (columns->StructSize != sizeof(NativeColumnSpecRaw))
            {
                return InvalidArgument($"fw_column_spec struct_size is {columns->StructSize}; expected {sizeof(NativeColumnSpecRaw)}.");
            }
            try
            {
                int status = NativeLayout.TryCreate(new ReadOnlySpan<NativeColumnSpecRaw>(columns, columnCount), options, out var layout);
                if (status == NativeStatus.Ok)
                {
                    *outLayout = NativeHandleTable.Register(layout!);
                }
                return status;
            }
            catch (Exception ex)
            {
                NativeApi.SetLastError(ex.Message);
                return NativeStatus.Error;
            }
        }

        [UnmanagedCallersOnly(EntryPoint = "fw_layout_free")]
        public static void LayoutFree(nint layout)
        {
            NativeHandleTable.TryUnregister<NativeLayout>(layout, out _);
        }

        private static int InvalidArgument(string message)
        {
            NativeApi.SetLastError(message);
            return NativeStatus.InvalidArgument;
        }
    }
}
