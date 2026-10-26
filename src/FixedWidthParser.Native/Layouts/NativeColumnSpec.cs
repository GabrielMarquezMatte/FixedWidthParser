using System.Runtime.InteropServices;

namespace FixedWidthParser.Native.Layouts
{
    /// <summary>The <c>FW_T_*</c> column types.</summary>
    internal static class NativeType
    {
        internal const int String = 0;
        internal const int Int64 = 1;
        internal const int Float64 = 2;
        internal const int Decimal = 3;
        internal const int Date = 4;
        internal const int Time = 5;
        internal const int Timestamp = 6;

        internal const int MaxScale = 18;

        /// <summary>The day DATE values count from, as Arrow's <c>date32</c> does.</summary>
        internal static readonly int EpochDay = new DateOnly(1970, 1, 1).DayNumber;

        /// <summary>10^scale for every scale a column may have.</summary>
        internal static readonly double[] Pow10 = [.. Enumerable.Range(0, MaxScale + 1).Select(p => Math.Pow(10, p))];
    }

    /// <summary><c>fw_column_spec</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NativeColumnSpecRaw
    {
        public int StructSize;
        public byte* Name;
        public int NameLen;
        public int Start;
        public int Length;
        public int Type;
        public int Nullable;
        public int Scale;
        public byte* Format;
        public int FormatLen;
        public int TrimChar;
        public int TrimMode;
        public int Alignment;
        public int Padding;
        public int Overflow;
        public int Dictionary;
    }

    /// <summary><c>fw_layout_options</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeLayoutOptionsRaw
    {
        public int StructSize;
        public int LineLength;
        public int Filler;
    }
}
