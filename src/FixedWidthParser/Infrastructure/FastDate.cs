namespace FixedWidthParser.Infrastructure
{
    /// <summary>
    /// A date or time format made only of fixed-width digit fields (<c>yyyy</c>, <c>MM</c>, <c>dd</c>, <c>HH</c>,
    /// <c>mm</c>, <c>ss</c>) and the literals <c>- . T / :</c>, read straight from the bytes the way ExcelReader's
    /// <c>FastDate</c> reads ISO 8601, and written the same way. <see cref="TryCreate"/> compiles it once per column
    /// and returns <see langword="null"/> for any other format; <see cref="TryParse"/> and <see cref="TryFormat"/>
    /// return <see langword="false"/> for anything they cannot do exactly as <c>TryParseExact</c> and <c>ToString</c>
    /// would, and the caller falls back to those.
    /// </summary>
    internal sealed class FastDate
    {
        private readonly string _format;
        private readonly int _year = -1;
        private readonly int _month = -1;
        private readonly int _day = -1;
        private readonly int _hour = -1;
        private readonly int _minute = -1;
        private readonly int _second = -1;
        // "/" and ":" stand for the provider's date and time separators, not for themselves.
        private readonly bool _usesSeparators;
        private readonly bool _valid;

        private FastDate(string format)
        {
            _format = format;
            for (int i = 0; i < format.Length;)
            {
                char c = format[i];
                int run = 1;
                while (i + run < format.Length && format[i + run] == c)
                {
                    run++;
                }
                switch (c, run)
                {
                    case ('y', 4) when _year < 0:
                        _year = i;
                        break;
                    case ('M', 2) when _month < 0:
                        _month = i;
                        break;
                    case ('d', 2) when _day < 0:
                        _day = i;
                        break;
                    case ('H', 2) when _hour < 0:
                        _hour = i;
                        break;
                    case ('m', 2) when _minute < 0:
                        _minute = i;
                        break;
                    case ('s', 2) when _second < 0:
                        _second = i;
                        break;
                    case ('-' or '.' or 'T', _):
                        break;
                    case ('/' or ':', _):
                        _usesSeparators = true;
                        break;
                    default:
                        return;
                }
                i += run;
            }
            // A date needs all of year, month and day, and a time its hour: TryParseExact fills a missing date
            // part from today, which a fixed result cannot match.
            bool date = _year >= 0 && _month >= 0 && _day >= 0;
            bool noDate = _year < 0 && _month < 0 && _day < 0;
            bool time = _hour >= 0 && (_second < 0 || _minute >= 0);
            bool noTime = _hour < 0 && _minute < 0 && _second < 0;
            _valid = (date || noDate) && (time || noTime) && (date || time);
        }

        internal bool HasDate => _year >= 0;

        internal bool HasTime => _hour >= 0;

        internal static FastDate? TryCreate(string? format)
        {
            if (format is null)
            {
                return null;
            }
            var fast = new FastDate(format);
            return fast._valid ? fast : null;
        }

        /// <summary>The date and time in <paramref name="s"/>, already trimmed; 0001-01-01 when the format has no date.</summary>
        internal bool TryParse(ReadOnlySpan<byte> s, in FixedWidthContext context, out DateTime value)
        {
            value = default;
            if (s.Length != _format.Length || !context.GregorianDates || (_usesSeparators && !context.StandardDateSeparators))
            {
                return false;
            }
            for (int i = 0; i < s.Length; i++)
            {
                char f = _format[i];
                bool matches = f is 'y' or 'M' or 'd' or 'H' or 'm' or 's' ? (uint)(s[i] - '0') <= 9 : s[i] == f;
                if (!matches)
                {
                    return false;
                }
            }
            int year = _year < 0 ? 1 : Digits(s, _year, 4);
            int month = _month < 0 ? 1 : Digits(s, _month, 2);
            int day = _day < 0 ? 1 : Digits(s, _day, 2);
            int hour = _hour < 0 ? 0 : Digits(s, _hour, 2);
            int minute = _minute < 0 ? 0 : Digits(s, _minute, 2);
            int second = _second < 0 ? 0 : Digits(s, _second, 2);
            if (year < 1 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)
                || hour > 23 || minute > 59 || second > 59)
            {
                return false;
            }
            // Unspecified, as TryParseExact returns text without a zone.
            value = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
            return true;
        }

        /// <summary>
        /// Writes <paramref name="value"/> as <c>ToString</c> with the format would, at the start of
        /// <paramref name="destination"/>; <see langword="false"/> when it does not fit or the provider's calendar or
        /// separators rule the fast path out.
        /// </summary>
        internal bool TryFormat(DateTime value, Span<byte> destination, in FixedWidthContext context, out int written)
        {
            written = 0;
            if (destination.Length < _format.Length || !context.GregorianDates || (_usesSeparators && !context.StandardDateSeparators))
            {
                return false;
            }
            for (int i = 0; i < _format.Length; i++)
            {
                destination[i] = (byte)_format[i];
            }
            WriteDigits(destination, _year, 4, value.Year);
            WriteDigits(destination, _month, 2, value.Month);
            WriteDigits(destination, _day, 2, value.Day);
            WriteDigits(destination, _hour, 2, value.Hour);
            WriteDigits(destination, _minute, 2, value.Minute);
            WriteDigits(destination, _second, 2, value.Second);
            written = _format.Length;
            return true;
        }

        private static void WriteDigits(Span<byte> destination, int offset, int count, int value)
        {
            if (offset < 0)
            {
                return;
            }
            for (int i = offset + count - 1; i >= offset; i--)
            {
                destination[i] = (byte)('0' + (value % 10));
                value /= 10;
            }
        }

        private static int Digits(ReadOnlySpan<byte> s, int offset, int count)
        {
            int value = 0;
            foreach (ref readonly byte b in s.Slice(offset, count))
            {
                value = (value * 10) + (b - '0');
            }
            return value;
        }
    }
}
