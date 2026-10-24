using System.Runtime.InteropServices;
using System.Text;
using FixedWidthParser.Native;
using FixedWidthParser.Native.Layouts;
using FixedWidthParser.Native.Reading;

namespace FixedWidthParser.Tests.Native
{
    /// <summary>A published <c>fw_table</c> copied into managed arrays, then freed.</summary>
    internal sealed class ReadOutcome
    {
        public int Status { get; init; }
        public long RowCount { get; init; }
        public NativeColumnRaw[] Columns { get; init; } = [];
        public List<object?[]> Values { get; init; } = [];
        public long LinesRead { get; init; }
        public long LinesSkipped { get; init; }
        public string Error { get; init; } = "";

        public object?[] Column(int index)
        {
            return Values[index];
        }
    }

    internal static unsafe class NativeTestReads
    {
        internal static NativeReadOptionsRaw Options(long headerLines = 0, bool skipInvalid = false, int degreeOfParallelism = 1, int encoding = 0)
        {
            return new NativeReadOptionsRaw
            {
                StructSize = sizeof(NativeReadOptionsRaw),
                HeaderLines = headerLines,
                SkipInvalid = skipInvalid ? 1 : 0,
                DegreeOfParallelism = degreeOfParallelism,
                Encoding = encoding,
            };
        }

        internal static ReadOutcome FromMemory(byte[] content, NativeLayout layout, NativeReadOptionsRaw? options = null, int chunkSize = 0)
        {
            NativeReadOptionsRaw value = options ?? default;
            NativeTableRaw table = default;
            var result = new NativeReadResultRaw { StructSize = sizeof(NativeReadResultRaw) };
            fixed (byte* data = content)
            {
                int status = ReadApi.ReadMemory(data, content.Length, layout, options is null ? null : &value, &table, &result, chunkSize);
                return Collect(status, &table, result);
            }
        }

        internal static ReadOutcome FromFile(byte[] content, NativeLayout layout, NativeReadOptionsRaw? options = null)
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(path, content);
                NativeReadOptionsRaw value = options ?? default;
                NativeTableRaw table = default;
                var result = new NativeReadResultRaw { StructSize = sizeof(NativeReadResultRaw) };
                int status = ReadApi.ReadFile(Encoding.UTF8.GetBytes(path), layout, options is null ? null : &value, &table, &result);
                return Collect(status, &table, result);
            }
            finally
            {
                File.Delete(path);
            }
        }

        internal static ReadOutcome[] Both(string content, NativeLayout layout, NativeReadOptionsRaw? options = null, Encoding? encoding = null)
        {
            byte[] bytes = (encoding ?? Encoding.UTF8).GetBytes(content);
            return [FromMemory(bytes, layout, options), FromFile(bytes, layout, options)];
        }

        internal static ReadOutcome Collect(int status, NativeTableRaw* table, NativeReadResultRaw result)
        {
            try
            {
                return new ReadOutcome
                {
                    Status = status,
                    RowCount = table->RowCount,
                    Columns = [.. Enumerable.Range(0, table->ColumnCount).Select(i => Column(table, i))],
                    Values = [.. Enumerable.Range(0, table->ColumnCount).Select(i => Copy(Column(table, i)))],
                    LinesRead = result.LinesRead,
                    LinesSkipped = result.LinesSkipped,
                    Error = status == NativeStatus.Ok ? "" : NativeTestLayouts.LastError(),
                };
            }
            finally
            {
                NativeTable.Free(table);
            }
        }

        private static NativeColumnRaw Column(NativeTableRaw* table, int index)
        {
            return table->Columns[index];
        }

        internal static object?[] Copy(NativeColumnRaw column)
        {
            var values = new object?[column.Length];
            for (int row = 0; row < column.Length; row++)
            {
                values[row] = IsNull(column, row) ? null : Value(column, row);
            }
            return values;
        }

        private static bool IsNull(NativeColumnRaw column, int row)
        {
            return column.Validity is not null && (column.Validity[row >> 3] & (1 << (row & 7))) == 0;
        }

        private static string Text(NativeColumnRaw column, int row)
        {
            int* offsets = (int*)column.Values;
            int length = offsets[row + 1] - offsets[row];
            return length == 0 ? "" : Encoding.UTF8.GetString(column.Data + offsets[row], length);
        }

        private static object Value(NativeColumnRaw column, int row)
        {
            return column.Type switch
            {
                NativeType.String => Text(column, row),
                NativeType.Float64 => ((double*)column.Values)[row],
                NativeType.Date => ((int*)column.Values)[row],
                _ => ((long*)column.Values)[row],
            };
        }
    }
}
