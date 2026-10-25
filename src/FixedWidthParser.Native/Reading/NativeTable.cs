using System.Runtime.InteropServices;

namespace FixedWidthParser.Native.Reading
{
    /// <summary><c>fw_column</c>: one column of a published table.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NativeColumnRaw
    {
        public int Type;
        public int Scale;
        public long Length;
        public void* Values;
        public byte* Validity;
        public byte* Data;
        public long DataLen;
    }

    /// <summary><c>fw_table</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NativeTableRaw
    {
        public int ColumnCount;
        public long RowCount;
        public NativeColumnRaw* Columns;
    }

    /// <summary><c>fw_read_options</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeReadOptionsRaw
    {
        public int StructSize;
        public long HeaderLines;
        public int SkipInvalid;
        public int DegreeOfParallelism;
        public int Encoding;
        public int MaxLineLength;
        public int SkipByteOrderMark;
    }

    /// <summary><c>fw_read_result</c>: an optional output, so its <c>struct_size</c> is checked, not read as options.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeReadResultRaw
    {
        public int StructSize;
        public long LinesRead;
        public long LinesSkipped;
    }

    internal static unsafe class NativeTable
    {
        /// <summary>Frees every buffer of a table published by the library and zeroes it; safe on a zeroed table.</summary>
        internal static void Free(NativeTableRaw* table)
        {
            if (table is null || table->Columns is null)
            {
                return;
            }
            for (int i = 0; i < table->ColumnCount; i++)
            {
                var column = table->Columns[i];
                Marshal.FreeHGlobal((IntPtr)column.Values);
                Marshal.FreeHGlobal((IntPtr)column.Validity);
                Marshal.FreeHGlobal((IntPtr)column.Data);
            }
            NativeMemory.Free(table->Columns);
            *table = default;
        }
    }
}
