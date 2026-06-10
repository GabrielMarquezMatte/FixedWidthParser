using System.Buffers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// UTF-8 / byte counterpart of <see cref="LineBufferState"/>: buffers raw bytes read from a
    /// <see cref="Stream"/> and splits them into lines as <see cref="ReadOnlySpan{T}"/> of
    /// <see cref="byte"/>, with no string (or <see cref="char"/> buffer) allocated per line. Splits on
    /// <c>\n</c>, dropping a preceding <c>\r</c> (CRLF), and skips a leading UTF-8 byte-order mark.
    /// </summary>
    internal struct Utf8LineBufferState
    {
        private const byte Cr = (byte)'\r';
        private const byte Lf = (byte)'\n';

        internal byte[] Buffer;
        internal int Start;
        internal int End;
        internal int ScanFrom;
        internal bool Eof;
        internal int LineNumber;
        private bool _bomChecked;

        internal void Rent(int bufferSize)
        {
            Buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            Start = 0;
            End = 0;
            ScanFrom = 0;
            Eof = false;
            LineNumber = 0;
            _bomChecked = false;
        }

        internal LineStatus TryGetLine(out ReadOnlySpan<byte> line)
        {
            // Skip a leading UTF-8 BOM (EF BB BF) before producing the first line. Needs at least 3
            // buffered bytes to decide; otherwise wait for more data (unless the stream already ended).
            if (!_bomChecked)
            {
                if (End - Start >= 3)
                {
                    if (Buffer[Start] == 0xEF && Buffer[Start + 1] == 0xBB && Buffer[Start + 2] == 0xBF)
                    {
                        Start += 3;
                        ScanFrom = Start;
                    }
                    _bomChecked = true;
                }
                else if (Eof)
                {
                    _bomChecked = true;
                }
                else
                {
                    line = default;
                    return LineStatus.NeedData;
                }
            }

            while (true)
            {
                if (ScanFrom < End)
                {
                    int rel = Buffer.AsSpan(ScanFrom, End - ScanFrom).IndexOf(Lf);
                    if (rel >= 0)
                    {
                        int nlIndex = ScanFrom + rel;
                        int contentEnd = nlIndex;
                        if (contentEnd > Start && Buffer[contentEnd - 1] == Cr)
                        {
                            contentEnd--;
                        }

                        line = Buffer.AsSpan(Start, contentEnd - Start);
                        Start = nlIndex + 1;
                        ScanFrom = Start;
                        LineNumber++;
                        if (!line.IsEmpty)
                        {
                            return LineStatus.Line;
                        }

                        continue;
                    }

                    ScanFrom = End;
                }

                if (Eof)
                {
                    if (Start < End)
                    {
                        int contentEnd = End;
                        if (contentEnd > Start && Buffer[contentEnd - 1] == Cr)
                        {
                            contentEnd--;
                        }

                        line = Buffer.AsSpan(Start, contentEnd - Start);
                        Start = End;
                        ScanFrom = End;
                        LineNumber++;
                        if (!line.IsEmpty)
                        {
                            return LineStatus.Line;
                        }
                    }

                    line = default;
                    return LineStatus.End;
                }

                line = default;
                return LineStatus.NeedData;
            }
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
            Start = 0;
        }

        internal bool GrowIfFull()
        {
            if (End != Buffer.Length)
            {
                return false;
            }

            var bigger = ArrayPool<byte>.Shared.Rent(Buffer.Length * 2);
            Array.Copy(Buffer, 0, bigger, 0, End);
            ArrayPool<byte>.Shared.Return(Buffer);
            Buffer = bigger;
            return true;
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
