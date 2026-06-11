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
    /// Element-type specifics for <see cref="LineBufferState{T, TFormat}"/>: the line-break markers
    /// and an optional preamble (BOM) to skip before the first line. Implemented by
    /// <see langword="struct"/>s so the calls devirtualize inside the generic buffer state, exactly
    /// like the <c>TParser</c> strategy on the enumerator cores.
    /// </summary>
    internal interface ILineFormat<T> where T : unmanaged
    {
        static abstract T Cr { get; }
        static abstract T Lf { get; }

        /// <summary>
        /// Decides how many leading elements to skip before the first line (e.g. a UTF-8 BOM).
        /// Returns <see langword="false"/> when more buffered data is needed to decide.
        /// </summary>
        static abstract bool TrySkipPreamble(ReadOnlySpan<T> data, bool eof, out int skip);
    }

    /// <summary>Char line format: no preamble — the <see cref="TextReader"/> already handled the BOM.</summary>
    internal readonly struct CharLineFormat : ILineFormat<char>
    {
        public static char Cr => '\r';
        public static char Lf => '\n';

        public static bool TrySkipPreamble(ReadOnlySpan<char> data, bool eof, out int skip)
        {
            skip = 0;
            return true;
        }
    }

    /// <summary>UTF-8 byte line format: skips a leading byte-order mark (EF BB BF).</summary>
    internal readonly struct Utf8LineFormat : ILineFormat<byte>
    {
        public static byte Cr => (byte)'\r';
        public static byte Lf => (byte)'\n';

        // Needs at least 3 buffered bytes to decide; otherwise wait for more data (unless the
        // stream already ended).
        public static bool TrySkipPreamble(ReadOnlySpan<byte> data, bool eof, out int skip)
        {
            if (data.Length >= 3)
            {
                skip = data is [0xEF, 0xBB, 0xBF, ..] ? 3 : 0;
                return true;
            }
            skip = 0;
            return eof;
        }
    }

    /// <summary>
    /// Buffers elements read in blocks from the underlying source and splits them into lines as
    /// <see cref="ReadOnlySpan{T}"/>, with no string (or per-line buffer) allocated. Splits on
    /// <see cref="ILineFormat{T}.Lf"/>, dropping a preceding <see cref="ILineFormat{T}.Cr"/> (CRLF),
    /// and skips the format's preamble before the first line. Used as
    /// <see cref="char"/>/<see cref="CharLineFormat"/> by the <see cref="TextReader"/>-based cores and
    /// as <see cref="byte"/>/<see cref="Utf8LineFormat"/> by the <see cref="Stream"/>-based UTF-8 cores.
    /// </summary>
    internal struct LineBufferState<T, TFormat>
        where T : unmanaged, IEquatable<T>
        where TFormat : struct, ILineFormat<T>
    {
        internal T[] Buffer;
        internal int Start;
        internal int End;
        internal int ScanFrom;
        internal bool Eof;
        internal int LineNumber;
        private bool _preambleChecked;

        internal void Rent(int bufferSize)
        {
            Buffer = ArrayPool<T>.Shared.Rent(bufferSize);
            Start = 0;
            End = 0;
            ScanFrom = 0;
            Eof = false;
            LineNumber = 0;
            _preambleChecked = false;
        }

        internal LineStatus TryGetLine(out ReadOnlySpan<T> line)
        {
            if (!_preambleChecked)
            {
                if (!TFormat.TrySkipPreamble(Buffer.AsSpan(Start, End - Start), Eof, out int skip))
                {
                    line = default;
                    return LineStatus.NeedData;
                }

                Start += skip;
                ScanFrom = Start;
                _preambleChecked = true;
            }

            while (true)
            {
                if (ScanFrom < End)
                {
                    int rel = Buffer.AsSpan(ScanFrom, End - ScanFrom).IndexOf(TFormat.Lf);
                    if (rel >= 0)
                    {
                        int nlIndex = ScanFrom + rel;
                        int contentEnd = nlIndex;
                        if (contentEnd > Start && Buffer[contentEnd - 1].Equals(TFormat.Cr))
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
                        if (contentEnd > Start && Buffer[contentEnd - 1].Equals(TFormat.Cr))
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

            var bigger = ArrayPool<T>.Shared.Rent(Buffer.Length * 2);
            Array.Copy(Buffer, 0, bigger, 0, End);
            ArrayPool<T>.Shared.Return(Buffer);
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
                ArrayPool<T>.Shared.Return(Buffer);
                Buffer = null!;
            }
        }
    }
}
