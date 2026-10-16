using System.Buffers;

namespace FixedWidthParser.Readers
{
    internal enum LineStatus
    {
        Line,
        NeedData,
        End
    }

    /// <summary>
    /// Buffers bytes read in blocks from a stream and splits them into lines as
    /// <see cref="ReadOnlySpan{T}"/> slices, with no per-line allocation. Splits on <c>\n</c>, dropping
    /// a preceding <c>\r</c>, skips a UTF-8 byte order mark when asked, and refuses a line longer than
    /// the maximum, which bounds how far the buffer grows on a source without line breaks.
    /// </summary>
    internal struct LineBufferState
    {
        internal byte[] Buffer;
        internal int Start;
        internal int End;
        internal int ScanFrom;
        internal bool Eof;
        internal long LineNumber;
        // Offset, from the first byte read, of the line TryGetLine last returned: the byte after the
        // previous line break, so a skipped byte order mark belongs to the first line.
        internal long LineStart;
        private long _nextLineStart;
        private long _compacted;
        private int _maxLineLength;
        private bool _preambleChecked;

        /// <summary>Offset, from the first byte read, of the first byte no line has consumed yet.</summary>
        internal readonly long Consumed => _compacted + Start;

        internal void Rent(FixedWidthReaderOptions options)
        {
            Rent(options, options.SkipsByteOrderMark);
        }

        internal void Rent(FixedWidthReaderOptions options, bool skipByteOrderMark)
        {
            Init(ArrayPool<byte>.Shared.Rent(options.BufferSize), 0, eof: false, linesBefore: 0, options, skipByteOrderMark);
        }

        /// <summary>
        /// Takes ownership of a pooled <paramref name="buffer"/> already holding the whole input: its
        /// first <paramref name="length"/> bytes, numbered from the line after <paramref name="linesBefore"/>.
        /// </summary>
        internal void Wrap(byte[] buffer, int length, long linesBefore, FixedWidthReaderOptions options, bool skipByteOrderMark)
        {
            Init(buffer, length, eof: true, linesBefore, options, skipByteOrderMark);
        }

        private void Init(byte[] buffer, int end, bool eof, long linesBefore, FixedWidthReaderOptions options, bool skipByteOrderMark)
        {
            Buffer = buffer;
            Start = 0;
            End = end;
            ScanFrom = 0;
            Eof = eof;
            LineNumber = linesBefore;
            LineStart = 0;
            _nextLineStart = 0;
            _compacted = 0;
            _maxLineLength = options.MaxLineLength;
            _preambleChecked = !skipByteOrderMark;
        }

        internal LineStatus TryGetLine(out ReadOnlySpan<byte> line)
        {
            if (!_preambleChecked)
            {
                if (End - Start < 3 && !Eof)
                {
                    line = default;
                    return LineStatus.NeedData;
                }
                if (Buffer.AsSpan(Start, End - Start) is [0xEF, 0xBB, 0xBF, ..])
                {
                    Start += 3;
                }
                ScanFrom = Start;
                _preambleChecked = true;
            }

            while (true)
            {
                if (ScanFrom < End)
                {
                    int rel = Buffer.AsSpan(ScanFrom, End - ScanFrom).IndexOf((byte)'\n');
                    if (rel >= 0)
                    {
                        int nlIndex = ScanFrom + rel;
                        if (TrySliceLine(rawEnd: nlIndex, nextStart: nlIndex + 1, out line))
                        {
                            return LineStatus.Line;
                        }

                        continue;
                    }

                    ScanFrom = End;
                }

                if (Eof)
                {
                    // Trailing content with no final '\n': the rest of the buffer is the last line.
                    if (Start < End && TrySliceLine(rawEnd: End, nextStart: End, out line))
                    {
                        return LineStatus.Line;
                    }

                    line = default;
                    return LineStatus.End;
                }

                line = default;
                return LineStatus.NeedData;
            }
        }

        /// <summary>
        /// Slices the line ending at <paramref name="rawEnd"/> (dropping a trailing <c>\r</c>), advances to
        /// <paramref name="nextStart"/> and counts the line. Returns <see langword="false"/> for an empty
        /// line so the caller skips it while still having consumed it.
        /// </summary>
        private bool TrySliceLine(int rawEnd, int nextStart, out ReadOnlySpan<byte> line)
        {
            int contentEnd = rawEnd;
            if (contentEnd > Start && Buffer[contentEnd - 1] == (byte)'\r')
            {
                contentEnd--;
            }

            if (contentEnd - Start > _maxLineLength)
            {
                throw ParseErrors.LineTooLong(LineNumber + 1L, _maxLineLength);
            }

            line = Buffer.AsSpan(Start, contentEnd - Start);
            LineStart = _nextLineStart;
            _nextLineStart = _compacted + nextStart;
            Start = nextStart;
            ScanFrom = nextStart;
            LineNumber++;
            return !line.IsEmpty;
        }

        internal void Compact()
        {
            if (Start <= 0)
            {
                return;
            }

            int len = End - Start;
            if (len > 0)
            {
                Array.Copy(Buffer, Start, Buffer, 0, len);
            }

            End = len;
            ScanFrom -= Start;
            _compacted += Start;
            Start = 0;
        }

        internal void GrowIfFull()
        {
            if (End != Buffer.Length)
            {
                return;
            }

            // A full buffer after Compact holds one unterminated line; +1 leaves room for its '\r'.
            if (End - Start > _maxLineLength + 1L)
            {
                throw ParseErrors.LineTooLong(LineNumber + 1L, _maxLineLength);
            }

            int size = (int)Math.Min(Buffer.Length * 2L, _maxLineLength + 2L);
            var bigger = ArrayPool<byte>.Shared.Rent(size);
            Array.Copy(Buffer, 0, bigger, 0, End);
            ArrayPool<byte>.Shared.Return(Buffer);
            Buffer = bigger;
        }

        internal void Advance(int read)
        {
            if (read == 0)
            {
                Eof = true;
            }
            else
            {
                End += read;
            }
        }

        internal void Return()
        {
            if (Buffer is not null)
            {
                ArrayPool<byte>.Shared.Return(Buffer);
                Buffer = null!;
            }
        }
    }
}
