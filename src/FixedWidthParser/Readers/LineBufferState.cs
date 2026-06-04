using System.Buffers;

namespace FixedWidthParser.Readers
{
    internal enum LineStatus
    {
        Line,
        NeedData,
        End
    }

    internal struct LineBufferState
    {
        internal char[] Buffer;
        internal int Start;
        internal int End;
        internal int ScanFrom;
        internal bool Eof;
        internal int LineNumber;

        internal void Rent(int bufferSize)
        {
            Buffer = ArrayPool<char>.Shared.Rent(bufferSize);
            Start = 0;
            End = 0;
            ScanFrom = 0;
            Eof = false;
            LineNumber = 0;
        }

        internal LineStatus TryGetLine(out ReadOnlySpan<char> line)
        {
            while (true)
            {
                if (ScanFrom < End)
                {
                    int rel = Buffer.AsSpan(ScanFrom, End - ScanFrom).IndexOf('\n');
                    if (rel >= 0)
                    {
                        int nlIndex = ScanFrom + rel;
                        int contentEnd = nlIndex;
                        if (contentEnd > Start && Buffer[contentEnd - 1] == '\r')
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
                        if (contentEnd > Start && Buffer[contentEnd - 1] == '\r')
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

            var bigger = ArrayPool<char>.Shared.Rent(Buffer.Length * 2);
            Array.Copy(Buffer, 0, bigger, 0, End);
            ArrayPool<char>.Shared.Return(Buffer);
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
                ArrayPool<char>.Shared.Return(Buffer);
                Buffer = null!;
            }
        }
    }
}
