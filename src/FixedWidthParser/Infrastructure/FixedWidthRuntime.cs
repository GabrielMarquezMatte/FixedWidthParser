using System.Buffers.Text;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Unicode;
using System.Text;
using FixedWidthParser.Processors;

namespace FixedWidthParser.Infrastructure
{
    /// <summary>
    /// Low-level helpers called by the source-generated <see cref="IFixedWidthModel{TSelf}.TryParse"/>
    /// and <see cref="IFixedWidthModel{TSelf}.TryFormat"/> implementations, and by the runtime parser
    /// and writer, so both paths share one set of semantics.
    /// <para>Public because the generated code lives in the consumer assembly; not intended for direct use.</para>
    /// </summary>
    public static class FixedWidthRuntime
    {
        private const int StackBufferSize = 256;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<byte> TrimColumn(ReadOnlySpan<byte> column, in ColumnParseOptions options)
        {
            return options.TrimMode switch
            {
                TrimMode.Leading => column.TrimStart(options.TrimByte),
                TrimMode.Both => column.Trim(options.TrimByte),
                _ => column.TrimEnd(options.TrimByte),
            };
        }

        /// <summary>True when the column holds only the trim character and spaces, so a nullable column reads as <see langword="null"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBlank(ReadOnlySpan<byte> column, in ColumnParseOptions options)
        {
            return column.Trim(options.TrimByte).Trim((byte)' ').IsEmpty;
        }

        /// <summary>Decodes a string column with the context's encoding, interning it through the context's pool when there is one.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string String(ReadOnlySpan<byte> column, in FixedWidthContext context, in ColumnParseOptions options)
        {
            var trimmed = TrimColumn(column, in options);
            return context.StringPool is { } pool ? pool.GetOrAdd(trimmed, context.Encoding) : context.Encoding.GetString(trimmed);
        }

        /// <summary>Parses a <see cref="double"/> column, honoring the provider's decimal separator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDouble(ReadOnlySpan<byte> column, in FixedWidthContext context, in ColumnParseOptions options, out double value)
        {
            return CultureHelpers.TryParseDouble(column, in context, in options, out value);
        }

        /// <summary>Parses a <see cref="float"/> column, honoring the provider's decimal separator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFloat(ReadOnlySpan<byte> column, in FixedWidthContext context, in ColumnParseOptions options, out float value)
        {
            return CultureHelpers.TryParseFloat(column, in context, in options, out value);
        }

        /// <summary>Parses an <see cref="IUtf8SpanParsable{TSelf}"/> column straight from its bytes.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse<TValue>(ReadOnlySpan<byte> column, in FixedWidthContext context, in ColumnParseOptions options, [MaybeNullWhen(false)] out TValue value)
            where TValue : IUtf8SpanParsable<TValue>
        {
            return TValue.TryParse(TrimColumn(column, in options), context.FormatProvider, out value);
        }

        /// <summary>Parses an <see cref="ISpanParsable{TSelf}"/> column that has no UTF-8 parser, decoding it with the context's encoding first.</summary>
        [SkipLocalsInit]
        public static bool TryParseDecoded<TValue>(ReadOnlySpan<byte> column, in FixedWidthContext context, in ColumnParseOptions options, [MaybeNullWhen(false)] out TValue value)
            where TValue : ISpanParsable<TValue>
        {
            Span<char> stack = stackalloc char[StackBufferSize];
            var text = Decode(TrimColumn(column, in options), context.Encoding, stack, out char[]? rented);
            try
            {
                return TValue.TryParse(text, context.FormatProvider, out value);
            }
            finally
            {
                Return(rented);
            }
        }

        /// <summary>Parses a column through an <see cref="IFixedWidthConverter{T}"/> instance.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryConvert<TValue, TConverter>(
            ReadOnlySpan<byte> column, in FixedWidthContext context, TConverter converter, in ColumnParseOptions options, [MaybeNullWhen(false)] out TValue value)
            where TConverter : IFixedWidthConverter<TValue>
        {
            return converter.TryParse(TrimColumn(column, in options), in context, out value);
        }

        /// <summary>Parses a <see cref="DateTime"/> column with exactly <see cref="ColumnParseOptions.Format"/>, decoding it first.</summary>
        [SkipLocalsInit]
        public static bool TryDateTimeExact(ReadOnlySpan<byte> column, in FixedWidthContext context, in ColumnParseOptions options, out DateTime value)
        {
            Span<char> stack = stackalloc char[StackBufferSize];
            var text = Decode(TrimColumn(column, in options), context.Encoding, stack, out char[]? rented);
            try
            {
                return DateTime.TryParseExact(text, options.Format, context.FormatProvider, DateTimeStyles.None, out value);
            }
            finally
            {
                Return(rented);
            }
        }

        /// <summary>Parses a <see cref="DateOnly"/> column with exactly <see cref="ColumnParseOptions.Format"/>, decoding it first.</summary>
        [SkipLocalsInit]
        public static bool TryDateOnlyExact(ReadOnlySpan<byte> column, in FixedWidthContext context, in ColumnParseOptions options, out DateOnly value)
        {
            Span<char> stack = stackalloc char[StackBufferSize];
            var text = Decode(TrimColumn(column, in options), context.Encoding, stack, out char[]? rented);
            try
            {
                return DateOnly.TryParseExact(text, options.Format, context.FormatProvider, DateTimeStyles.None, out value);
            }
            finally
            {
                Return(rented);
            }
        }

        /// <summary>Parses a <see cref="TimeOnly"/> column with exactly <see cref="ColumnParseOptions.Format"/>, decoding it first.</summary>
        [SkipLocalsInit]
        public static bool TryTimeOnlyExact(ReadOnlySpan<byte> column, in FixedWidthContext context, in ColumnParseOptions options, out TimeOnly value)
        {
            Span<char> stack = stackalloc char[StackBufferSize];
            var text = Decode(TrimColumn(column, in options), context.Encoding, stack, out char[]? rented);
            try
            {
                return TimeOnly.TryParseExact(text, options.Format, context.FormatProvider, DateTimeStyles.None, out value);
            }
            finally
            {
                Return(rented);
            }
        }

        /// <summary>Parses a <see cref="DateTimeOffset"/> column with exactly <see cref="ColumnParseOptions.Format"/>, decoding it first.</summary>
        [SkipLocalsInit]
        public static bool TryDateTimeOffsetExact(ReadOnlySpan<byte> column, in FixedWidthContext context, in ColumnParseOptions options, out DateTimeOffset value)
        {
            Span<char> stack = stackalloc char[StackBufferSize];
            var text = Decode(TrimColumn(column, in options), context.Encoding, stack, out char[]? rented);
            try
            {
                return DateTimeOffset.TryParseExact(text, options.Format, context.FormatProvider, DateTimeStyles.None, out value);
            }
            finally
            {
                Return(rented);
            }
        }

        /// <summary>
        /// Encodes a string column into <paramref name="slice"/> per <paramref name="options"/>. A string
        /// truncated to fit keeps whole characters, so a multi-byte character is never split.
        /// </summary>
        public static void FormatString(string value, Span<byte> slice, in FixedWidthContext context, ColumnFormatOptions options, string columnName)
        {
            // Left-aligned UTF-8 transcodes straight into the slice; FromUtf16 stops at a whole character,
            // which is exactly the truncation. Overflow that throws falls through to report it.
            // ponytail: lone surrogates become U+FFFD here even for a throwing UTF8Encoding.
            if (context.IsUtf8 && options.Alignment == Alignment.Left)
            {
                var status = Utf8.FromUtf16(value, slice, out _, out int written);
                if (status == OperationStatus.Done || options.Overflow == OverflowBehavior.Truncate)
                {
                    slice[written..].Fill((byte)options.Padding);
                    return;
                }
            }

            ReadOnlySpan<char> text = value;
            var encoding = context.Encoding;
            if (options.Overflow == OverflowBehavior.Truncate && encoding.GetByteCount(text) > slice.Length)
            {
                text = FitText(text, slice.Length, encoding, options.Alignment);
            }
            WriteText(text, slice, encoding, options, columnName);
        }

        /// <summary>
        /// Formats an <see cref="ISpanFormattable"/> column into <paramref name="slice"/> per
        /// <paramref name="options"/>: tries a stack buffer first, then grows via a bounded
        /// <see cref="ArrayPool{T}"/> loop for the rare value that needs more room.
        /// </summary>
        [SkipLocalsInit]
        public static void FormatValue<TValue>(TValue value, Span<byte> slice, in FixedWidthContext context, ColumnFormatOptions options, string columnName)
            where TValue : ISpanFormattable
        {
            var formatProvider = context.FormatProvider;
            Span<char> stack = stackalloc char[64];
            if (value.TryFormat(stack, out int written, options.Format, formatProvider))
            {
                WriteText(stack[..written], slice, context.Encoding, options, columnName);
                return;
            }

            int maxSize = Math.Max(1024, slice.Length * 16);
            for (int size = 512; size <= maxSize; size *= 2)
            {
                char[] rented = ArrayPool<char>.Shared.Rent(size);
                try
                {
                    if (value.TryFormat(rented, out int w, options.Format, formatProvider))
                    {
                        WriteText(rented.AsSpan(0, w), slice, context.Encoding, options, columnName);
                        return;
                    }
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(rented);
                }
            }

            throw new InvalidOperationException(
                $"Value of type \"{typeof(TValue)}\" for column \"{columnName}\" could not be formatted " +
                $"within {maxSize} characters (format \"{options.Format}\").");
        }

        /// <summary>
        /// Formats an <see cref="IUtf8SpanFormattable"/> column straight into <paramref name="slice"/>, with
        /// no intermediate buffer or transcode. The UTF-8 result is kept when the encoding is UTF-8 or the
        /// result is ASCII (numbers and dates, almost always); otherwise, and for a value wider than the
        /// column, it falls back to <see cref="FormatValue{TValue}"/>, which re-encodes or reports the overflow.
        /// </summary>
        public static void FormatUtf8Value<TValue>(TValue value, Span<byte> slice, in FixedWidthContext context, ColumnFormatOptions options, string columnName)
            where TValue : IUtf8SpanFormattable, ISpanFormattable
        {
            if (TryFormatInto(value, slice, in context, options, out int written) && (context.IsUtf8 || Ascii.IsValid(slice[..written])))
            {
                options.AlignInPlace(slice, written);
                return;
            }
            FormatValue(value, slice, in context, options, columnName);
        }

        private static bool TryFormatInto<TValue>(TValue value, Span<byte> destination, in FixedWidthContext context, ColumnFormatOptions options, out int written)
            where TValue : IUtf8SpanFormattable
        {
            if (typeof(TValue) == typeof(double) && options.Format is null)
            {
                return TryFormatDouble(Unsafe.As<TValue, double>(ref value), destination, in context, out written);
            }
            return value.TryFormat(destination, out written, options.Format, context.FormatProvider);
        }

        /// <summary>
        /// Byte-identical to <see cref="double.TryFormat(Span{byte}, out int, ReadOnlySpan{char}, IFormatProvider?)"/>
        /// with no format. A value with at most four fractional digits and a magnitude under 1e9 is written
        /// as a scaled integer; the smallest scale whose quotient reproduces the value exactly is the
        /// shortest round-trip form.
        /// </summary>
        internal static bool TryFormatDouble(double value, Span<byte> destination, in FixedWidthContext context, out int written)
        {
            byte separator = context.DecimalSeparator;
            if (separator != 0 && value != 0 && Math.Abs(value) < 1e9)
            {
                ReadOnlySpan<double> pow10 = [1, 10, 100, 1000, 10000];
                for (int scale = 0; scale < pow10.Length; scale++)
                {
                    double scaled = Math.Round(value * pow10[scale]);
                    if (scaled / pow10[scale] == value)
                    {
                        return TryFormatScaled((long)scaled, scale, separator, destination, out written);
                    }
                }
            }
            return value.TryFormat(destination, out written, default, context.FormatProvider);
        }

        private static bool TryFormatScaled(long scaled, int scale, byte separator, Span<byte> destination, out int written)
        {
            written = 0;
            if (scaled < 0)
            {
                if (destination.IsEmpty)
                {
                    return false;
                }
                destination[written++] = (byte)'-';
            }
            ulong magnitude = (ulong)Math.Abs(scaled);
            ulong divisor = scale switch { 0 => 1, 1 => 10, 2 => 100, 3 => 1000, _ => 10000 };
            if (!Utf8Formatter.TryFormat(magnitude / divisor, destination[written..], out int n))
            {
                return false;
            }
            written += n;
            if (scale == 0)
            {
                return true;
            }
            if (destination.Length < written + 1 + scale)
            {
                return false;
            }
            destination[written++] = separator;
            Utf8Formatter.TryFormat(magnitude % divisor, destination[written..], out n, new StandardFormat('D', (byte)scale));
            written += n;
            return true;
        }

        /// <summary>
        /// Formats a column via a <c>FixedColumnAttribute.Converter</c> instance into <paramref name="slice"/>
        /// per <paramref name="options"/>, with the same stack-then-ArrayPool growth as <see cref="FormatValue{TValue}"/>.
        /// </summary>
        [SkipLocalsInit]
        public static void FormatConvert<TValue, TConverter>(
            TValue value, Span<byte> slice, in FixedWidthContext context, TConverter converter, ColumnFormatOptions options, string columnName)
            where TConverter : IFixedWidthConverter<TValue>
        {
            Span<byte> stack = stackalloc byte[StackBufferSize];
            if (converter.TryFormat(value, stack, in context, out int written))
            {
                options.WriteInto(stack[..written], slice, columnName);
                return;
            }

            int maxSize = Math.Max(4096, slice.Length * 16);
            for (int size = 1024; size <= maxSize; size *= 2)
            {
                byte[] rented = ArrayPool<byte>.Shared.Rent(size);
                try
                {
                    if (converter.TryFormat(value, rented, in context, out int w))
                    {
                        options.WriteInto(rented.AsSpan(0, w), slice, columnName);
                        return;
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }

            throw new InvalidOperationException(
                $"Value of type \"{typeof(TValue)}\" for column \"{columnName}\" could not be formatted by " +
                $"converter \"{typeof(TConverter)}\" within {maxSize} bytes.");
        }

        internal static byte ToAsciiByte(char value, string columnName, string setting)
        {
            if (value > '\x7F')
            {
                throw new InvalidOperationException(
                    $"The {setting} '{value}' (U+{(int)value:X4}) of column \"{columnName}\" is not an ASCII character; column positions are bytes, so it must be one byte in every supported encoding.");
            }
            return (byte)value;
        }

        // Numbers and dates are ASCII in almost every culture, so they narrow straight into bytes;
        // anything else goes through the encoding.
        [SkipLocalsInit]
        private static void WriteText(ReadOnlySpan<char> text, Span<byte> slice, Encoding encoding, ColumnFormatOptions options, string columnName)
        {
            bool ascii = Ascii.IsValid(text);
            int byteCount = ascii ? text.Length : encoding.GetByteCount(text);
            byte[]? rented = byteCount <= StackBufferSize ? null : ArrayPool<byte>.Shared.Rent(byteCount);
            Span<byte> bytes = rented is null ? stackalloc byte[StackBufferSize] : rented;
            try
            {
                int written = ascii
                    ? NarrowAscii(text, bytes)
                    : encoding.GetBytes(text, bytes);
                options.WriteInto(bytes[..written], slice, columnName);
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }

        private static int NarrowAscii(ReadOnlySpan<char> text, Span<byte> bytes)
        {
            Ascii.FromUtf16(text, bytes, out int written);
            return written;
        }

        // The longest run of whole characters, from the alignment side, whose encoding fits in width bytes.
        private static ReadOnlySpan<char> FitText(ReadOnlySpan<char> text, int width, Encoding encoding, Alignment alignment)
        {
            int bytes = 0;
            if (alignment == Alignment.Right)
            {
                int start = text.Length;
                while (start > 0)
                {
                    Rune.DecodeLastFromUtf16(text[..start], out _, out int consumed);
                    int size = encoding.GetByteCount(text.Slice(start - consumed, consumed));
                    if (bytes + size > width)
                    {
                        break;
                    }
                    bytes += size;
                    start -= consumed;
                }
                return text[start..];
            }

            int end = 0;
            while (end < text.Length)
            {
                Rune.DecodeFromUtf16(text[end..], out _, out int consumed);
                int size = encoding.GetByteCount(text.Slice(end, consumed));
                if (bytes + size > width)
                {
                    break;
                }
                bytes += size;
                end += consumed;
            }
            return text[..end];
        }

        private static ReadOnlySpan<char> Decode(ReadOnlySpan<byte> bytes, Encoding encoding, Span<char> stack, out char[]? rented)
        {
            rented = null;
            Span<char> chars = stack;
            int max = encoding.GetMaxCharCount(bytes.Length);
            if (max > stack.Length)
            {
                rented = ArrayPool<char>.Shared.Rent(max);
                chars = rented;
            }
            int written = encoding.GetChars(bytes, chars);
            return chars[..written];
        }

        private static void Return(char[]? rented)
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }
}
