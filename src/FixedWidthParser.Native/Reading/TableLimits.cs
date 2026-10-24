using System.Runtime.InteropServices;

namespace FixedWidthParser.Native.Reading
{
    /// <summary>
    /// What one published table may hold: its row count and each string column's bytes must fit the int32
    /// lengths and offsets of <c>fw_table</c> and Arrow <c>utf8</c>. Tests lower them to reach the limits.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct TableLimits(long MaxTextBytes, long MaxRows)
    {
        internal static TableLimits Default => new(int.MaxValue, int.MaxValue);

        internal InvalidOperationException TooManyRows()
        {
            return new InvalidOperationException($"The table has more than {MaxRows} rows; read in batches (fw_reader_open_*).");
        }
    }
}
