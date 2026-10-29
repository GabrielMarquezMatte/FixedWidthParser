using System.Globalization;
using System.Text;
using FixedWidthParser.Infrastructure;
using FixedWidthParser.Native.Layouts;
using FixedWidthParser.Native.Reading;

namespace FixedWidthParser.Native.Writing
{
    /// <summary>
    /// Formats the rows of a caller's <c>fw_table</c> through a layout, one line at a time into a slice the
    /// caller owns. Read-only over the table, so several threads may format different rows at once.
    /// </summary>
    internal sealed unsafe class TableFormatter
    {
        private readonly NativeColumn[] _columns;
        private readonly NativeColumnRaw* _data;
        private readonly byte _filler;
        private readonly FixedWidthContext _context;

        private TableFormatter(NativeLayout layout, NativeTableRaw* table, Encoding? encoding)
        {
            _columns = layout.Columns;
            _data = table->Columns;
            _filler = layout.Filler;
            _context = new FixedWidthContext { FormatProvider = CultureInfo.InvariantCulture, Encoding = encoding! };
            Rows = table->RowCount;
        }

        internal long Rows { get; }

        internal static int TryCreate(NativeTableRaw* table, NativeLayout layout, Encoding? encoding, out TableFormatter? formatter)
        {
            formatter = null;
            string? error = Check(table, layout);
            if (error is not null)
            {
                return NativeApi.InvalidArgument(error);
            }
            formatter = new TableFormatter(layout, table, encoding);
            return NativeStatus.Ok;
        }

        private static string? Check(NativeTableRaw* table, NativeLayout layout)
        {
            var columns = layout.Columns;
            if (table->ColumnCount != columns.Length || table->RowCount < 0 || (table->Columns is null && table->ColumnCount > 0))
            {
                return $"The table has {table->ColumnCount} columns and the layout {columns.Length}; they must match by position.";
            }
            for (int i = 0; i < columns.Length; i++)
            {
                var raw = table->Columns[i];
                var column = columns[i];
                if (raw.Type != column.Type || (column.Type == NativeType.Decimal && raw.Scale != column.Scale))
                {
                    return $"Column '{column.Name}' has type {raw.Type} (scale {raw.Scale}) in the table and {column.Type} (scale {column.Scale}) in the layout.";
                }
                if (raw.Length != table->RowCount || (raw.Values is null && raw.Length > 0) || (raw.Data is null && raw.DataLen > 0) || raw.DataLen < 0)
                {
                    return $"Column '{column.Name}' has {raw.Length} values for {table->RowCount} rows, or a buffer that does not match its length.";
                }
                if (column.Type == NativeType.String && !OffsetsWithinData(raw))
                {
                    return $"Column '{column.Name}' has string offsets that descend or fall outside its {raw.DataLen} data bytes.";
                }
            }
            return null;
        }

        /// <summary>True when a string column's offsets ascend within <c>0..DataLen</c>, so no row reads past its data.</summary>
        private static bool OffsetsWithinData(NativeColumnRaw raw)
        {
            if (raw.Length == 0)
            {
                return true;
            }
            var offsets = (int*)raw.Values;
            if (offsets[0] < 0)
            {
                return false;
            }
            for (long row = 0; row < raw.Length; row++)
            {
                if (offsets[row + 1] < offsets[row])
                {
                    return false;
                }
            }
            return offsets[raw.Length] <= raw.DataLen;
        }

        /// <summary>Writes row <paramref name="row"/> into <paramref name="line"/>, whose length is the layout's line length.</summary>
        internal void FormatRow(long row, Span<byte> line)
        {
            line.Fill(_filler);
            for (int i = 0; i < _columns.Length; i++)
            {
                var column = _columns[i];
                var slice = line.Slice(column.Start, column.Length);
                var raw = _data + i;
                if (raw->Validity is not null && (raw->Validity[row >> 3] & (1 << (int)(row & 7))) == 0)
                {
                    slice.Fill((byte)column.Parse.TrimChar);
                    continue;
                }
                FormatValue(column, raw, row, slice);
            }
        }

        private void FormatValue(NativeColumn column, NativeColumnRaw* raw, long row, Span<byte> slice)
        {
            var options = column.FormatOptions;
            switch (column.Type)
            {
                case NativeType.String:
                    var offsets = (int*)raw->Values;
                    FormatText(column, new ReadOnlySpan<byte>(raw->Data + offsets[row], offsets[row + 1] - offsets[row]), slice);
                    return;
                case NativeType.Float64:
                    FormatFloat(column, ((double*)raw->Values)[row], slice);
                    return;
                case NativeType.Date:
                    FixedWidthRuntime.FormatUtf8Value(DateOnly.FromDayNumber(NativeType.EpochDay + ((int*)raw->Values)[row]), slice, in _context, options, column.Name);
                    return;
                case NativeType.Time:
                    FixedWidthRuntime.FormatUtf8Value(new TimeOnly(Ticks(column, raw, row, 0, TimeSpan.TicksPerDay - 1)), slice, in _context, options, column.Name);
                    return;
                case NativeType.Timestamp:
                    long epoch = DateTime.UnixEpoch.Ticks;
                    var ticks = epoch + Ticks(column, raw, row, DateTime.MinValue.Ticks - epoch, DateTime.MaxValue.Ticks - epoch);
                    FixedWidthRuntime.FormatUtf8Value(new DateTime(ticks, DateTimeKind.Unspecified), slice, in _context, options, column.Name);
                    return;
                case NativeType.Decimal:
                    long unscaled = ((long*)raw->Values)[row];
                    // The reader takes at most 18 digits; writing more would make a line it cannot read back.
                    if (unscaled is <= -NativeType.DecimalLimit or >= NativeType.DecimalLimit)
                    {
                        throw new InvalidOperationException($"Value {unscaled} of column \"{column.Name}\" has more than 18 digits, which a DECIMAL cannot read back.");
                    }
                    FixedWidthRuntime.FormatUtf8Value(unscaled, slice, in _context, options, column.Name);
                    return;
                default:
                    FixedWidthRuntime.FormatUtf8Value(((long*)raw->Values)[row], slice, in _context, options, column.Name);
                    return;
            }
        }

        /// <summary>The row's microseconds as ticks, checked against <paramref name="min"/>..<paramref name="max"/> before scaling so it cannot wrap.</summary>
        private static long Ticks(NativeColumn column, NativeColumnRaw* raw, long row, long min, long max)
        {
            long micros = ((long*)raw->Values)[row];
            if (micros < min / TimeSpan.TicksPerMicrosecond || micros > max / TimeSpan.TicksPerMicrosecond)
            {
                throw new InvalidOperationException($"Value {micros} of column \"{column.Name}\" is out of range in microseconds.");
            }
            return micros * TimeSpan.TicksPerMicrosecond;
        }

        private void FormatFloat(NativeColumn column, double value, Span<byte> slice)
        {
            if (column.Scale == 0)
            {
                FixedWidthRuntime.FormatUtf8Value(value, slice, in _context, column.FormatOptions, column.Name);
                return;
            }
            double scaled = Math.Round(value * NativeType.Pow10[column.Scale]);
            if (!(Math.Abs(scaled) < 9e18))
            {
                throw new InvalidOperationException($"Value {value} of column \"{column.Name}\" does not fit a scaled 64-bit integer.");
            }
            FixedWidthRuntime.FormatUtf8Value((long)scaled, slice, in _context, column.FormatOptions, column.Name);
        }

        private void FormatText(NativeColumn column, ReadOnlySpan<byte> utf8, Span<byte> slice)
        {
            var options = column.FormatOptions;
            if (!_context.IsUtf8)
            {
                FixedWidthRuntime.FormatString(Encoding.UTF8.GetString(utf8), slice, in _context, options, column.Name);
                return;
            }
            if (utf8.Length > slice.Length && options.Overflow == OverflowBehavior.Truncate)
            {
                utf8 = WholeCharacters(utf8, slice.Length, options.Alignment == Alignment.Right);
            }
            options.WriteInto(utf8, slice, column.Name);
        }

        /// <summary>At most <paramref name="width"/> bytes of <paramref name="utf8"/>, from the aligned side, never splitting a character.</summary>
        private static ReadOnlySpan<byte> WholeCharacters(ReadOnlySpan<byte> utf8, int width, bool keepEnd)
        {
            if (keepEnd)
            {
                int start = utf8.Length - width;
                while (start < utf8.Length && (utf8[start] & 0xC0) == 0x80)
                {
                    start++;
                }
                return utf8[start..];
            }
            int end = width;
            while (end > 0 && (utf8[end] & 0xC0) == 0x80)
            {
                end--;
            }
            return utf8[..end];
        }
    }
}
