using System.Runtime.ExceptionServices;
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
        private readonly TableLimits _limits;
#pragma warning disable SS066 // Shared by every partition; the read that made it disposes it.
        private readonly TableArena? _arena;
#pragma warning restore SS066
        private int _failedColumn;
        private List<TableSink>? _following;

        /// <summary>
        /// Initializes a new instance of the <see cref="TableSink"/> struct.
        /// </summary>
        /// <param name="arena">The final buffers to parse into, shared by every partition; null for buffers of its own.</param>
        /// <param name="start">Where the bytes this sink will see start, if known: fixed-width lines make them a row count.</param>
        /// <param name="end">Where those bytes end.</param>
        internal unsafe TableSink(NativeLayout layout, TableLimits limits, TableArena? arena = null, long start = 0, long end = 0)
        {
            _layout = layout;
            _limits = limits;
            _arena = arena;
            int firstRow = arena?.RowAt(start) ?? 0;
            long expectedRows = arena is null
                ? Math.Min((end - start) / (layout.LineLength + 1), limits.MaxRows)
                : arena.RowAt(end) - firstRow;
            _columns = new ColumnBuilder[layout.Columns.Length];
            for (int i = 0; i < _columns.Length; i++)
            {
                _columns[i] = new ColumnBuilder(layout.Columns[i], limits.MaxTextBytes, expectedRows, arena is null ? null : arena.Lend(i, firstRow));
            }
            _requiredLength = layout.Columns.Max(c => c.Start + c.Length);
            _failedColumn = -1;
            _following = null;
            Rows = 0;
        }

        /// <summary>The rows of this partition only.</summary>
        internal int Rows { get; private set; }

        /// <summary>The rows of this partition and every partition merged into it.</summary>
        internal readonly long TotalRows => Rows + (_following?.Sum(f => (long)f.Rows) ?? 0);

        public bool TryAdd(ReadOnlySpan<byte> line, in FixedWidthContext context)
        {
            if (line.Length < _requiredLength)
            {
                _failedColumn = -1;
                return false;
            }
            if (Rows >= _limits.MaxRows)
            {
                throw _limits.TooManyRows();
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

        /// <summary>Adopts the following partition without copying; <see cref="Publish"/> concatenates once.</summary>
        public void Merge(TableSink following)
        {
            (_following ??= []).Add(following);
        }

        /// <summary>This partition's columns, then every merged partition's, in file order.</summary>
        internal readonly IEnumerable<ColumnBuilder[]> Chunks => _following is null ? [_columns] : [_columns, .. _following.Select(f => f._columns)];

        /// <summary>Hands every column's buffers to <paramref name="table"/>; the caller frees them with <see cref="NativeTable.Free"/>.</summary>
        /// <remarks>Fails before allocating when the merged partitions exceed the limits; frees what it built if anything else fails.</remarks>
        internal readonly unsafe void Publish(NativeTableRaw* table)
        {
            var chunks = Chunks.ToArray();
            CheckLimits(chunks);
            var columns = (NativeColumnRaw*)NativeMemory.AllocZeroed((nuint)_columns.Length, (nuint)sizeof(NativeColumnRaw));
            table->Columns = columns;
            table->ColumnCount = _columns.Length;
            try
            {
                if (chunks.Length == 1 && _arena is null)
                {
                    for (int i = 0; i < _columns.Length; i++)
                    {
                        chunks[0][i].Publish(columns + i);
                    }
                }
                else
                {
                    ConcatInParallel(chunks, columns, _arena);
                }
                table->RowCount = TotalRows;
            }
            catch
            {
                NativeTable.Free(table);
                throw;
            }
        }

        // A concatenated column is all fresh memory, and its page faults are most of the copy's cost: one
        // column per thread spreads them out.
        private static unsafe void ConcatInParallel(ColumnBuilder[][] chunks, NativeColumnRaw* columns, TableArena? arena)
        {
            nint target = (nint)columns;
            try
            {
                Parallel.For(0, chunks[0].Length, i =>
                {
                    var parts = new ColumnBuilder[chunks.Length];
                    for (int c = 0; c < chunks.Length; c++)
                    {
                        parts[c] = chunks[c][i];
                    }
                    ColumnBuilder.Concat(parts, (NativeColumnRaw*)target + i, arena?.Block(i));
                });
            }
            catch (AggregateException ex)
            {
                ExceptionDispatchInfo.Throw(ex.InnerExceptions[0]);
            }
        }

        private readonly void CheckLimits(ColumnBuilder[][] chunks)
        {
            if (TotalRows > _limits.MaxRows)
            {
                throw _limits.TooManyRows();
            }
            for (int i = 0; i < _columns.Length; i++)
            {
                long text = chunks.Sum(chunk => chunk[i].TextBytes);
                if (text > _limits.MaxTextBytes)
                {
                    throw ColumnBuilder.TooMuchText(_layout.Columns[i].Name, _limits.MaxTextBytes);
                }
            }
        }

        public readonly void Dispose()
        {
            foreach (var chunk in Chunks)
            {
                foreach (var column in chunk)
                {
                    column.Dispose();
                }
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
