using System.Runtime.InteropServices;
using FixedWidthParser.Native.Layouts;
using FixedWidthParser.Readers;

namespace FixedWidthParser.Native.Reading
{
    /// <summary>One partition's columns: each accepted line appends one value to every <see cref="ColumnBuilder"/>.</summary>
    internal struct TableSink : ILineSink<TableSink>, IDisposable
    {
        private readonly NativeLayout _layout;
        private readonly ColumnBuilder[] _columns;
        private readonly int _requiredLength;
        private int _failedColumn;

        internal TableSink(NativeLayout layout)
        {
            _layout = layout;
            _columns = [.. layout.Columns.Select(c => new ColumnBuilder(c))];
            _requiredLength = layout.Columns.Max(c => c.Start + c.Length);
            _failedColumn = -1;
            Rows = 0;
        }

        internal int Rows { get; private set; }

        public bool TryAdd(ReadOnlySpan<byte> line, in FixedWidthContext context)
        {
            if (line.Length < _requiredLength)
            {
                _failedColumn = -1;
                return false;
            }
            var columns = _layout.Columns;
            for (int i = 0; i < _columns.Length; i++)
            {
                if (!_columns[i].TryAppend(line.Slice(columns[i].Start, columns[i].Length), in context))
                {
                    Reject(i);
                    return false;
                }
            }
            Rows++;
            return true;
        }

        public readonly FixedWidthParseException Diagnose(ReadOnlySpan<byte> line, long lineNumber, in FixedWidthContext context)
        {
            if (_failedColumn < 0)
            {
                return new FixedWidthParseException(
                    $"Line {lineNumber} could not be parsed: it is {line.Length} bytes long and the layout needs {_requiredLength}.",
                    lineNumber, columnName: null, columnStart: -1, columnLength: 0, rawValue: null);
            }
            var column = _layout.Columns[_failedColumn];
            string raw = context.Encoding.GetString(line.Slice(column.Start, column.Length));
            return new FixedWidthParseException(
                $"Line {lineNumber} could not be parsed: column '{column.Name}' (start {column.Start}, length {column.Length}) holds \"{raw}\".",
                lineNumber, column.Name, column.Start, column.Length, raw);
        }

        public readonly void Merge(TableSink following)
        {
            throw new NotSupportedException("Partitioned reads arrive in a later step.");
        }

        /// <summary>Hands every column's buffers to <paramref name="table"/>; the caller frees them with <see cref="NativeTable.Free"/>.</summary>
        internal readonly unsafe void Publish(NativeTableRaw* table)
        {
            var columns = (NativeColumnRaw*)Marshal.AllocHGlobal(_columns.Length * sizeof(NativeColumnRaw));
            for (int i = 0; i < _columns.Length; i++)
            {
                _columns[i].Publish(columns + i);
            }
            table->ColumnCount = _columns.Length;
            table->RowCount = Rows;
            table->Columns = columns;
        }

        public readonly void Dispose()
        {
            foreach (var column in _columns)
            {
                column.Dispose();
            }
        }

        private void Reject(int failed)
        {
            _failedColumn = failed;
            foreach (ref readonly var column in _columns.AsSpan(0, failed))
            {
                column.Truncate(Rows);
            }
        }
    }
}
