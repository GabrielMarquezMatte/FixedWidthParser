using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace FixedWidthParser.Native
{
    /// <summary>
    /// The <c>[UnmanagedCallersOnly]</c> entry points: pointer checks and marshaling only, so the logic
    /// stays in the span-based layer the tests drive. One partial per feature.
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal static unsafe partial class Exports
    {
        [UnmanagedCallersOnly(EntryPoint = "fw_abi_version")]
        public static int AbiVersion()
        {
            return NativeStatus.AbiVersion;
        }

        [UnmanagedCallersOnly(EntryPoint = "fw_last_error")]
        public static int LastError(byte* buffer, int capacity, int* outLength)
        {
            if (outLength is null || capacity < 0 || (buffer is null && capacity > 0))
            {
                return NativeStatus.InvalidArgument;
            }
            return NativeApi.LastError(new Span<byte>(buffer, capacity), out *outLength);
        }

        [UnmanagedCallersOnly(EntryPoint = "fw_last_parse_error")]
        public static int LastParseError(NativeParseError* outError)
        {
            if (outError is null || outError->StructSize != sizeof(NativeParseError))
            {
                return NativeStatus.InvalidArgument;
            }
            NativeApi.LastParseError(out outError->LineNumber, out outError->Column);
            return NativeStatus.Ok;
        }
    }

    /// <summary><c>fw_parse_error</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeParseError
    {
        public int StructSize;
        public long LineNumber;
        public int Column;
    }
}
