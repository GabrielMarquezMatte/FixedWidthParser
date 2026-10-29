using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;
using FixedWidthParser.Infrastructure;
using FixedWidthParser.Native.Layouts;

namespace FixedWidthParser.Native.Reading
{
    /// <summary>
    /// One column of one partition, parsed straight into native buffers that are handed to the caller as they
    /// are. Values: <c>long</c> for INT64, DECIMAL, TIME and TIMESTAMP, <c>double</c> for FLOAT64, <c>int</c>
    /// for DATE and for STRING offsets into UTF-8 bytes. The validity bitmap exists only once a null arrives.
    /// </summary>
    internal sealed unsafe class ColumnBuilder : IDisposable
    {
        private const int StackChars = 256;
        private const DateTimeStyles Utc = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;

        private readonly NativeColumn _column;
        private readonly long _maxTextBytes;
        private readonly GrowableNativeBuffer<long> _longs = new();
        private readonly GrowableNativeBuffer<double> _doubles = new();
        private readonly GrowableNativeBuffer<int> _ints = new();
        private readonly GrowableNativeBuffer<byte> _bytes = new();
#pragma warning disable SS066 // Disposed in Dispose, or detached by Publish.
        private GrowableNativeBuffer<byte>? _validity;
#pragma warning restore SS066

        /// <summary>
        /// Initializes a new instance of the <see cref="ColumnBuilder"/> class.
        /// </summary>
        /// <param name="column">The native column this builder is for.</param>
        /// <param name="maxTextBytes">The maximum number of text bytes expected for this column.</param>
        /// <param name="expectedRows">How many rows to size the buffers for up front, so a partition does not grow them row by row.</param>
        /// <param name="lent">Room for <paramref name="expectedRows"/> values that a <see cref="TableArena"/> owns, or null.</param>
        /// <param name="lentOffsets">For a STRING column, room for its <paramref name="expectedRows"/> + 1 offsets, or null.</param>
        /// <param name="firstSlice">Whether <paramref name="lentOffsets"/> starts the column: only the first slice writes its leading 0.</param>
        internal ColumnBuilder(NativeColumn column, long maxTextBytes, long expectedRows, void* lent = null, int* lentOffsets = null, bool firstSlice = true)
        {
            _column = column;
            _maxTextBytes = maxTextBytes;
            switch (column.Type)
            {
                case NativeType.String:
                    Size(_ints, lentOffsets, expectedRows + 1);
                    Size(_bytes, lent, Math.Min(expectedRows * column.Length, maxTextBytes));
                    // A later slice's index 0 is the previous slice's last offset, which that slice writes: this one
                    // claims it without writing, and reads it nowhere (Truncate knows row 0 starts at 0).
                    if (lentOffsets is null || firstSlice)
                    {
                        _ints.Add(0);
                    }
                    else
                    {
                        _ints.Append(1);
                    }
                    break;
                case NativeType.Float64:
                    Size(_doubles, lent, expectedRows);
                    break;
                case NativeType.Date:
                    Size(_ints, lent, expectedRows);
                    break;
                default:
                    Size(_longs, lent, expectedRows);
                    break;
            }
        }

        private static void Size<T>(GrowableNativeBuffer<T> values, void* lent, long rows) where T : unmanaged
        {
            if (lent is null)
            {
                values.Reserve(rows);
                return;
            }
            values.Borrow((T*)lent, (int)rows);
        }

        /// <summary>The bytes of one value of a column of <paramref name="type"/>; 0 for STRING, which has no fixed size.</summary>
        internal static int ValueSize(int type)
        {
            return type switch
            {
                NativeType.String => 0,
                NativeType.Date => sizeof(int),
                _ => sizeof(long),
            };
        }

        internal int Rows { get; private set; }

        /// <summary>The UTF-8 bytes a STRING column holds.</summary>
        internal long TextBytes => _bytes.Count;

        internal bool TryAppend(ReadOnlySpan<byte> raw, in FixedWidthContext context)
        {
            var parse = _column.Parse;
            if (_column.Nullable && FixedWidthRuntime.IsBlank(raw, in parse))
            {
                AppendNull();
                return true;
            }
            bool parsed = _column.Type switch
            {
                NativeType.String => AppendString(FixedWidthRuntime.TrimColumn(raw, in parse), in context),
                NativeType.Int64 => Add(FixedWidthRuntime.TryParse(raw, in context, in parse, out long integer), integer),
                NativeType.Float64 => AppendFloat(raw, in context, in parse),
                NativeType.Decimal => Add(TryDecimal(FixedWidthRuntime.TrimColumn(raw, in parse), _column.Scale, out long unscaled), unscaled),
                _ => AppendTemporal(raw, in context, in parse),
            };
            if (parsed)
            {
                SetValid(Rows, true);
                Rows++;
            }
            return parsed;
        }

        /// <summary>Drops every row from <paramref name="rows"/> on, undoing the columns of a line rejected midway.</summary>
        internal void Truncate(int rows)
        {
            Rows = rows;
            _longs.Truncate(Math.Min(_longs.Count, rows));
            _doubles.Truncate(Math.Min(_doubles.Count, rows));
            if (_column.Type != NativeType.String)
            {
                _ints.Truncate(Math.Min(_ints.Count, rows));
                return;
            }
            _ints.Truncate(rows + 1);
            // Index 0 of a lent slice belongs to the slice before; row 0 always starts at 0.
            _bytes.Truncate(rows == 0 ? 0 : _ints[rows]);
        }

        internal void Publish(NativeColumnRaw* target)
        {
            target->Type = _column.Type;
            target->Scale = _column.Scale;
            target->Length = Rows;
            target->DataLen = _bytes.Count;
            target->Validity = _validity is null ? null : (byte*)_validity.Detach();
            target->Data = _column.Type == NativeType.String ? (byte*)_bytes.Detach() : null;
            target->Values = (void*)(_column.Type switch
            {
                NativeType.Float64 => _doubles.Detach(),
                NativeType.String or NativeType.Date => _ints.Detach(),
                _ => _longs.Detach(),
            });
        }

        /// <summary>
        /// Publishes the same column of several partitions, in order, as one column. Partitions that parsed into slices
        /// of <paramref name="lent"/> are moved down in it to close the gaps between them, and it becomes the column;
        /// otherwise, or once a partition outgrew its slice, they are copied into fresh memory.
        /// </summary>
        /// <param name="lentOffsets">The block a STRING column's offsets were lent from, closed up and handed over the same way.</param>
        internal static void Concat(ReadOnlySpan<ColumnBuilder> parts, NativeColumnRaw* target, GrowableNativeBuffer<byte>? lent = null,
            GrowableNativeBuffer<int>? lentOffsets = null)
        {
            long rows = 0;
            foreach (ref readonly var part in parts)
            {
                rows += part.Rows;
                lent = part.Lent ? lent : null;
                lentOffsets = part._ints.Borrowed ? lentOffsets : null;
            }
            if (parts.Length == 1 && lent is null && lentOffsets is null)
            {
                parts[0].Publish(target);
                return;
            }
            var column = parts[0]._column;
            target->Type = column.Type;
            target->Scale = column.Scale;
            target->Length = rows;
            target->Validity = ConcatValidity(parts, rows);
            target->Data = null;
            target->DataLen = 0;
            if (column.Type == NativeType.String)
            {
                ConcatStrings(parts, rows, target, lent, lentOffsets);
                return;
            }
            target->Values = ConcatValues(parts, lent);
        }

        /// <summary>Whether this partition's values, or a STRING column's bytes, still sit where a <see cref="TableArena"/> lent them.</summary>
        private bool Lent => _column.Type switch
        {
            NativeType.String => _bytes.Borrowed,
            NativeType.Float64 => _doubles.Borrowed,
            NativeType.Date => _ints.Borrowed,
            _ => _longs.Borrowed,
        };

        private ReadOnlySpan<byte> ValueBytes => _column.Type switch
        {
            NativeType.Float64 => MemoryMarshal.AsBytes(_doubles.Span),
            NativeType.Date => MemoryMarshal.AsBytes(_ints.Span),
            _ => MemoryMarshal.AsBytes(_longs.Span),
        };

        private static void* ConcatValues(ReadOnlySpan<ColumnBuilder> parts, GrowableNativeBuffer<byte>? lent)
        {
            long total = 0;
            foreach (ref readonly var part in parts)
            {
                total += part.ValueBytes.Length;
            }
            byte* values = lent is null ? (byte*)Marshal.AllocHGlobal((nint)total) : lent.Pointer;
            long at = 0;
            foreach (ref readonly var part in parts)
            {
                var bytes = part.ValueBytes;
                MoveTo(bytes, values + at);
                at += bytes.Length;
            }
            return lent is null ? values : (void*)lent.Detach();
        }

        private static void ConcatStrings(ReadOnlySpan<ColumnBuilder> parts, long rows, NativeColumnRaw* target, GrowableNativeBuffer<byte>? lent,
            GrowableNativeBuffer<int>? lentOffsets)
        {
            long total = 0;
            foreach (ref readonly var part in parts)
            {
                total += part._bytes.Count;
            }
            if (total > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Column '{parts[0]._column.Name}' holds more than 2 GiB of text; read in batches (fw_reader_open_*).");
            }
            byte* data = lent is null ? (byte*)Marshal.AllocHGlobal((nint)total) : lent.Pointer;
            // Lent offsets are rebased in place, moving down in order as values do; one partition of its own already
            // starts at 0; several of their own are copied into fresh memory.
            int* offsets = null;
            if (lentOffsets is not null)
            {
                offsets = lentOffsets.Pointer;
            }
            else if (parts.Length > 1)
            {
                offsets = (int*)Marshal.AllocHGlobal((nint)((rows + 1) * sizeof(int)));
            }
            if (offsets is not null)
            {
                offsets[0] = 0;
            }
            long row = 1;
            int baseOffset = 0;
            foreach (ref readonly var part in parts)
            {
                if (offsets is not null)
                {
                    foreach (ref readonly int offset in part._ints.Span[1..])
                    {
                        offsets[row++] = baseOffset + offset;
                    }
                }
                MoveTo(part._bytes.Span, data + baseOffset);
                baseOffset += part._bytes.Count;
            }
            if (lentOffsets is not null)
            {
                offsets = (int*)lentOffsets.Detach();
            }
            target->Values = offsets is null ? (void*)parts[0]._ints.Detach() : offsets;
            if (lent is not null)
            {
                // The arena lent every row the column's full width; trimmed text is often far shorter, and the table
                // would keep the difference for its lifetime. Every partition has moved in by now.
                lent.TrimExcess((int)total);
                data = (byte*)lent.Detach();
            }
            target->Data = data;
            target->DataLen = total;
        }

        /// <summary>
        /// Copies <paramref name="bytes"/> to <paramref name="to"/>, which may overlap them: partitions lent slices of one
        /// buffer move down in order, each staying inside its own slice, so none overwrites one not yet moved.
        /// </summary>
        private static void MoveTo(ReadOnlySpan<byte> bytes, byte* to)
        {
            fixed (byte* from = bytes)
            {
                if (from != to)
                {
                    bytes.CopyTo(new Span<byte>(to, bytes.Length));
                }
            }
        }

        private static byte* ConcatValidity(ReadOnlySpan<ColumnBuilder> parts, long rows)
        {
            bool any = false;
            foreach (ref readonly var part in parts)
            {
                any |= part._validity is not null;
            }
            if (!any)
            {
                return null;
            }
            long length = (rows + 7) / 8;
            byte* bits = (byte*)Marshal.AllocHGlobal((nint)length);
            new Span<byte>(bits, (int)length).Fill(0xFF);
            long row = 0;
            foreach (ref readonly var part in parts)
            {
                var partBits = part._validity is null ? default : part._validity.Span;
                int r = 0;
                // A partition starting on a byte boundary copies its whole bytes; its last few rows, and a partition
                // starting mid-byte, go bit by bit, so no padding bit lands on another partition's rows.
                if (!partBits.IsEmpty && (row & 7) == 0)
                {
                    r = part.Rows & ~7;
                    partBits[..(r >> 3)].CopyTo(new Span<byte>(bits + (row >> 3), r >> 3));
                }
                for (; r < part.Rows && !partBits.IsEmpty; r++)
                {
                    if ((partBits[r >> 3] & (1 << (r & 7))) == 0)
                    {
                        bits[(row + r) >> 3] &= (byte)~(1 << (int)((row + r) & 7));
                    }
                }
                row += part.Rows;
            }
            return bits;
        }

        public void Dispose()
        {
            _longs.Dispose();
            _doubles.Dispose();
            _ints.Dispose();
            _bytes.Dispose();
            _validity?.Dispose();
        }

        private bool Add(bool parsed, long value)
        {
            if (parsed)
            {
                _longs.Add(value);
            }
            return parsed;
        }

        private bool AppendFloat(ReadOnlySpan<byte> raw, in FixedWidthContext context, in ColumnParseOptions parse)
        {
            var text = FixedWidthRuntime.TrimColumn(raw, in parse);
            if (_column.Scale > 0 && TryInteger(text, out long digits))
            {
                _doubles.Add(digits / NativeType.Pow10[_column.Scale]);
                return true;
            }
            if (!FixedWidthRuntime.TryDouble(raw, in context, in parse, out double value))
            {
                return false;
            }
            // Digits too many for a long still carry the implied decimals.
            bool impliedDecimals = _column.Scale > 0 && text.IndexOfAny(".,eE"u8) < 0;
            _doubles.Add(impliedDecimals ? value / NativeType.Pow10[_column.Scale] : value);
            return true;
        }

        private static bool TryInteger(ReadOnlySpan<byte> text, out long value)
        {
            return FastInteger.TryParse(text.Trim((byte)' '), allowMinus: true, maxDigits: 18, out value)
                || long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryDecimal(ReadOnlySpan<byte> text, int scale, out long unscaled)
        {
            if (TryInteger(text, out unscaled))
            {
                return unscaled is > -NativeType.DecimalLimit and < NativeType.DecimalLimit;
            }
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
            {
                return false;
            }
            // Bounded before scaling: a value near decimal's range would overflow the product and throw, not fail the parse.
            decimal factor = (decimal)NativeType.Pow10[scale];
            if (Math.Abs(value) >= NativeType.DecimalLimit / factor)
            {
                return false;
            }
            decimal scaled = value * factor;
            if (scaled != decimal.Truncate(scaled))
            {
                return false;
            }
            unscaled = (long)scaled;
            return true;
        }

        private bool AppendTemporal(ReadOnlySpan<byte> raw, in FixedWidthContext context, in ColumnParseOptions parse)
        {
            bool exact = parse.Format is not null;
            switch (_column.Type)
            {
                case NativeType.Date:
                    bool dateOk = exact ? FixedWidthRuntime.TryDateOnlyExact(raw, in context, in parse, out var date) : FixedWidthRuntime.TryParseDecoded(raw, in context, in parse, out date);
                    if (dateOk)
                    {
                        _ints.Add(date.DayNumber - NativeType.EpochDay);
                    }
                    return dateOk;
                case NativeType.Time:
                    bool timeOk = exact ? FixedWidthRuntime.TryTimeOnlyExact(raw, in context, in parse, out var time) : FixedWidthRuntime.TryParseDecoded(raw, in context, in parse, out time);
                    return Add(timeOk, time.Ticks / TimeSpan.TicksPerMicrosecond);
                default:
                    // A fast parse has no zone, which the slow path would read as UTC too.
                    var text = FixedWidthRuntime.TrimColumn(raw, in parse);
                    bool stampOk = (parse.FastDate is { HasDate: true } fast && fast.TryParse(text, in context, out var stamp))
                        || TryTimestamp(text, context.Encoding, parse.Format, out stamp);
                    return Add(stampOk, (stamp.Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerMicrosecond);
            }
        }

        /// <summary>A timestamp in UTC: a zone or offset in the text is applied, and text without one is taken as UTC.</summary>
        private static bool TryTimestamp(ReadOnlySpan<byte> text, Encoding encoding, string? format, out DateTime value)
        {
            value = default;
            if (encoding.GetMaxCharCount(text.Length) > StackChars && encoding.GetCharCount(text) > StackChars)
            {
                return false;
            }
            Span<char> chars = stackalloc char[StackChars];
            chars = chars[..encoding.GetChars(text, chars)];
            return format is null
                ? DateTime.TryParse(chars, CultureInfo.InvariantCulture, Utc, out value)
                : DateTime.TryParseExact(chars, format, CultureInfo.InvariantCulture, Utc, out value);
        }

        private bool AppendString(ReadOnlySpan<byte> text, in FixedWidthContext context)
        {
            // Invalid UTF-8 (a split character, or another encoding read as UTF-8) is decoded with U+FFFD, as the core does.
            if (!context.IsUtf8 || !Utf8.IsValid(text))
            {
                AppendTranscoded(text, context.IsUtf8 ? Encoding.UTF8 : context.Encoding);
                _ints.Add(_bytes.Count);
                return true;
            }
            EnsureTextRoom(text.Length);
            _bytes.AddRange(text);
            _ints.Add(_bytes.Count);
            return true;
        }

        private void AppendTranscoded(ReadOnlySpan<byte> text, Encoding encoding)
        {
            int charCount = encoding.GetCharCount(text);
            char[]? rented = charCount > StackChars ? ArrayPool<char>.Shared.Rent(charCount) : null;
            try
            {
                Span<char> chars = rented ?? stackalloc char[StackChars];
                chars = chars[..encoding.GetChars(text, chars)];
                int byteCount = Encoding.UTF8.GetByteCount(chars);
                EnsureTextRoom(byteCount);
                Encoding.UTF8.GetBytes(chars, _bytes.Append(byteCount));
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<char>.Shared.Return(rented);
                }
            }
        }

        private void EnsureTextRoom(int bytes)
        {
            if (_bytes.Count + (long)bytes > _maxTextBytes)
            {
                throw TooMuchText(_column.Name, _maxTextBytes);
            }
        }

        internal static InvalidOperationException TooMuchText(string column, long limit)
        {
            return new InvalidOperationException(
                $"Column '{column}' holds more than {limit} bytes of text; read in batches (fw_reader_open_*).");
        }

        private void AppendNull()
        {
            switch (_column.Type)
            {
                case NativeType.String:
                    _ints.Add(_bytes.Count);
                    break;
                case NativeType.Float64:
                    _doubles.Add(0);
                    break;
                case NativeType.Date:
                    _ints.Add(0);
                    break;
                default:
                    _longs.Add(0);
                    break;
            }
            SetValid(Rows, false);
            Rows++;
        }

        private void SetValid(int row, bool valid)
        {
            if (_validity is null)
            {
                if (valid)
                {
                    return;
                }
                _validity = new GrowableNativeBuffer<byte>();
                _validity.Append((row >> 3) + 1).Fill(0xFF);
            }
            while (_validity.Count <= row >> 3)
            {
                _validity.Add(0xFF);
            }
            ref byte bits = ref _validity[row >> 3];
            int mask = 1 << (row & 7);
            bits = (byte)(valid ? bits | mask : bits & ~mask);
        }
    }
}
