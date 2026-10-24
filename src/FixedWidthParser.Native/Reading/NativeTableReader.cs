using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FixedWidthParser.Native.Layouts;
using FixedWidthParser.Readers;

namespace FixedWidthParser.Native.Reading
{
    /// <summary><c>fw_reader_options</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeReaderOptionsRaw
    {
        public int StructSize;
        public NativeReadOptionsRaw Read;
        public long BatchRows;
    }

    /// <summary>
    /// A sequential read handed out a table of at most <c>batch_rows</c> rows at a time, behind
    /// <c>fw_reader_open_*</c>. Used by one thread at a time; it keeps its layout alive after
    /// <c>fw_layout_free</c>.
    /// </summary>
    internal sealed unsafe class NativeTableReader : IDisposable
    {
        private const long DefaultBatchRows = 65_536;

        private readonly Stream _stream;
        private readonly NativeLayout _layout;
        private readonly FixedWidthReaderOptions _options;
        private readonly FixedWidthContext _context;
        private readonly long _batchRows;
        private LineBufferState _lines;
        private bool _done;

        internal NativeTableReader(Stream stream, long dataStart, long headerLines, NativeLayout layout, ReadSettings settings, long batchRows)
        {
            _stream = stream;
            _stream.Position = dataStart;
            _layout = layout;
            _options = settings.ReaderOptions(new StrongBox<long>(), degreeOfParallelism: 1, chunkSize: 0);
            _context = _options.Context;
            _batchRows = batchRows == 0 ? DefaultBatchRows : batchRows;
            _lines = default;
            _lines.Rent(_options, dataStart == 0 && _options.SkipsByteOrderMark);
            _lines.LineNumber = headerLines;
        }

        /// <summary>Publishes the next batch into <paramref name="table"/>, or returns <see cref="NativeStatus.Eof"/>; a failure ends the read.</summary>
        internal int Next(NativeTableRaw* table)
        {
            if (_done)
            {
                return NativeStatus.Eof;
            }
            var sink = new TableSink(_layout, TableLimits.Default);
            try
            {
#pragma warning disable IDISP003 // Fill appends to the sink; it never replaces it.
                Fill(ref sink);
#pragma warning restore IDISP003
                if (sink.Rows == 0)
                {
                    return NativeStatus.Eof;
                }
                sink.Publish(table);
                return NativeStatus.Ok;
            }
            catch
            {
                _done = true;
                throw;
            }
            finally
            {
                sink.Dispose();
            }
        }

        private void Fill(ref TableSink sink)
        {
            while (sink.Rows < _batchRows)
            {
                var status = _lines.TryGetLine(out var line);
                if (status == LineStatus.End)
                {
                    _done = true;
                    return;
                }
                if (status == LineStatus.NeedData)
                {
                    _lines.Fill(_stream);
                    continue;
                }
                if (!sink.TryAdd(line, in _context))
                {
                    ParseErrors.Reject(sink.Diagnose(line, _lines.LineNumber, in _context), _options.OnParseError);
                }
            }
        }

        internal NativeLayout Layout => _layout;

        public void Dispose()
        {
            _lines.Return();
#pragma warning disable IDISP007 // The reader owns the stream it was opened over.
            _stream.Dispose();
#pragma warning restore IDISP007
        }
    }
}
