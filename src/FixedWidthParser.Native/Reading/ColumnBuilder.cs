using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
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
        private static readonly int EpochDay = new DateOnly(1970, 1, 1).DayNumber;
        private static readonly double[] Pow10 = [.. Enumerable.Range(0, NativeType.MaxScale + 1).Select(p => Math.Pow(10, p))];

        private readonly NativeColumn _column;
        private readonly GrowableNativeBuffer<long> _longs = new();
        private readonly GrowableNativeBuffer<double> _doubles = new();
        private readonly GrowableNativeBuffer<int> _ints = new();
        private readonly GrowableNativeBuffer<byte> _bytes = new();
#pragma warning disable SS066 // Disposed in Dispose, or detached by Publish.
        private GrowableNativeBuffer<byte>? _validity;
#pragma warning restore SS066

        internal ColumnBuilder(NativeColumn column)
        {
            _column = column;
            if (column.Type == NativeType.String)
            {
                _ints.Add(0);
            }
        }

        internal int Rows { get; private set; }

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
            _bytes.Truncate(_ints[rows]);
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

        /// <summary>Publishes the same column of several partitions, in order, as one column: the one copy a partitioned read makes.</summary>
        internal static void Concat(ReadOnlySpan<ColumnBuilder> parts, NativeColumnRaw* target)
        {
            if (parts.Length == 1)
            {
                parts[0].Publish(target);
                return;
            }
            long rows = 0;
            foreach (ref readonly var part in parts)
            {
                rows += part.Rows;
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
                ConcatStrings(parts, rows, target);
                return;
            }
            target->Values = ConcatValues(parts);
        }

        private ReadOnlySpan<byte> ValueBytes => _column.Type switch
        {
            NativeType.Float64 => MemoryMarshal.AsBytes(_doubles.Span),
            NativeType.Date => MemoryMarshal.AsBytes(_ints.Span),
            _ => MemoryMarshal.AsBytes(_longs.Span),
        };

        private static void* ConcatValues(ReadOnlySpan<ColumnBuilder> parts)
        {
            long total = 0;
            foreach (ref readonly var part in parts)
            {
                total += part.ValueBytes.Length;
            }
            byte* values = (byte*)Marshal.AllocHGlobal((nint)total);
            long at = 0;
            foreach (ref readonly var part in parts)
            {
                var bytes = part.ValueBytes;
                bytes.CopyTo(new Span<byte>(values + at, bytes.Length));
                at += bytes.Length;
            }
            return values;
        }

        private static void ConcatStrings(ReadOnlySpan<ColumnBuilder> parts, long rows, NativeColumnRaw* target)
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
            int* offsets = (int*)Marshal.AllocHGlobal((nint)((rows + 1) * sizeof(int)));
            byte* data = (byte*)Marshal.AllocHGlobal((nint)total);
            offsets[0] = 0;
            long row = 1;
            int baseOffset = 0;
            foreach (ref readonly var part in parts)
            {
                var partOffsets = part._ints.Span;
                foreach (ref readonly int offset in partOffsets[1..])
                {
                    offsets[row++] = baseOffset + offset;
                }
                part._bytes.Span.CopyTo(new Span<byte>(data + baseOffset, part._bytes.Count));
                baseOffset += part._bytes.Count;
            }
            target->Values = offsets;
            target->Data = data;
            target->DataLen = total;
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
                for (int r = 0; r < part.Rows && !partBits.IsEmpty; r++)
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
            if (_column.Scale > 0 && long.TryParse(FixedWidthRuntime.TrimColumn(raw, in parse), NumberStyles.Integer, CultureInfo.InvariantCulture, out long digits))
            {
                _doubles.Add(digits / Pow10[_column.Scale]);
                return true;
            }
            if (!FixedWidthRuntime.TryDouble(raw, in context, in parse, out double value))
            {
                return false;
            }
            _doubles.Add(value);
            return true;
        }

        private static bool TryDecimal(ReadOnlySpan<byte> text, int scale, out long unscaled)
        {
            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out unscaled))
            {
                return true;
            }
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
            {
                return false;
            }
            decimal scaled = value * (decimal)Pow10[scale];
            if (scaled != decimal.Truncate(scaled) || scaled is < long.MinValue or > long.MaxValue)
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
                        _ints.Add(date.DayNumber - EpochDay);
                    }
                    return dateOk;
                case NativeType.Time:
                    bool timeOk = exact ? FixedWidthRuntime.TryTimeOnlyExact(raw, in context, in parse, out var time) : FixedWidthRuntime.TryParseDecoded(raw, in context, in parse, out time);
                    return Add(timeOk, time.Ticks / TimeSpan.TicksPerMicrosecond);
                default:
                    bool stampOk = exact ? FixedWidthRuntime.TryDateTimeExact(raw, in context, in parse, out var stamp) : FixedWidthRuntime.TryParseDecoded(raw, in context, in parse, out stamp);
                    return Add(stampOk, (stamp.Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerMicrosecond);
            }
        }

        private bool AppendString(ReadOnlySpan<byte> text, in FixedWidthContext context)
        {
            if (!context.IsUtf8)
            {
                AppendTranscoded(text, context.Encoding);
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
            if ((long)_bytes.Count + bytes > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Column '{_column.Name}' holds more than 2 GiB of text; read in batches (fw_reader_open_*).");
            }
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
