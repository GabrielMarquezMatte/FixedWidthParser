using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using FixedWidthParser.Attributes;
using FixedWidthParser.Formatters;
using FixedWidthParser.Processors;

namespace FixedWidthParser
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
        public static ReadOnlySpan<byte> TrimColumn(ReadOnlySpan<byte> column, byte trimChar, TrimMode trimMode)
        {
            return trimMode switch
            {
                TrimMode.Leading => column.TrimStart(trimChar),
                TrimMode.Both => column.Trim(trimChar),
                _ => column.TrimEnd(trimChar),
            };
        }

        /// <summary>True when the column holds only <paramref name="trimChar"/> and spaces, so a nullable column reads as <see langword="null"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBlank(ReadOnlySpan<byte> column, byte trimChar)
        {
            return column.Trim(trimChar).Trim((byte)' ').IsEmpty;
        }

        /// <summary>Decodes a string column with the context's encoding, interning it through the context's pool when there is one.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string String(ReadOnlySpan<byte> column, in FixedWidthContext context, byte trimChar = (byte)' ', TrimMode trimMode = TrimMode.Trailing)
        {
            var trimmed = TrimColumn(column, trimChar, trimMode);
            return context.StringPool is { } pool ? pool.GetOrAdd(trimmed, context.Encoding) : context.Encoding.GetString(trimmed);
        }

        /// <summary>Parses a <see cref="double"/> column, honoring the provider's decimal separator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDouble(ReadOnlySpan<byte> column, in FixedWidthContext context, out double value, byte trimChar = (byte)' ', TrimMode trimMode = TrimMode.Trailing)
        {
            return CultureHelpers.TryParseDouble(column, in context, out value, trimChar, trimMode);
        }

        /// <summary>Parses a <see cref="float"/> column, honoring the provider's decimal separator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFloat(ReadOnlySpan<byte> column, in FixedWidthContext context, out float value, byte trimChar = (byte)' ', TrimMode trimMode = TrimMode.Trailing)
        {
            return CultureHelpers.TryParseFloat(column, in context, out value, trimChar, trimMode);
        }

        /// <summary>Parses an <see cref="IUtf8SpanParsable{TSelf}"/> column straight from its bytes.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse<TValue>(ReadOnlySpan<byte> column, in FixedWidthContext context, [MaybeNullWhen(false)] out TValue value, byte trimChar = (byte)' ', TrimMode trimMode = TrimMode.Trailing)
            where TValue : IUtf8SpanParsable<TValue>
        {
            return TValue.TryParse(TrimColumn(column, trimChar, trimMode), context.FormatProvider, out value);
        }

        /// <summary>Parses an <see cref="ISpanParsable{TSelf}"/> column that has no UTF-8 parser, decoding it with the context's encoding first.</summary>
        [SkipLocalsInit]
        public static bool TryParseDecoded<TValue>(ReadOnlySpan<byte> column, in FixedWidthContext context, [MaybeNullWhen(false)] out TValue value, byte trimChar = (byte)' ', TrimMode trimMode = TrimMode.Trailing)
            where TValue : ISpanParsable<TValue>
        {
            Span<char> stack = stackalloc char[StackBufferSize];
            var text = Decode(TrimColumn(column, trimChar, trimMode), context.Encoding, stack, out char[]? rented);
            try
            {
                return TValue.TryParse(text, context.FormatProvider, out value);
            }
            finally
            {
                Return(rented);
            }
        }

        /// <summary>Parses a column via a <c>FixedColumnAttribute.Converter</c> instance.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryConvert<TValue, TConverter>(
            ReadOnlySpan<byte> column, in FixedWidthContext context, TConverter converter, [MaybeNullWhen(false)] out TValue value, byte trimChar = (byte)' ', TrimMode trimMode = TrimMode.Trailing)
            where TConverter : IFixedWidthConverter<TValue>
        {
            return converter.TryParse(TrimColumn(column, trimChar, trimMode), in context, out value);
        }

        [SkipLocalsInit]
        public static bool TryDateTimeExact(ReadOnlySpan<byte> column, string format, in FixedWidthContext context, out DateTime value, byte trimChar = (byte)' ', TrimMode trimMode = TrimMode.Trailing)
        {
            Span<char> stack = stackalloc char[StackBufferSize];
            var text = Decode(TrimColumn(column, trimChar, trimMode), context.Encoding, stack, out char[]? rented);
            try
            {
                return DateTime.TryParseExact(text, format, context.FormatProvider, DateTimeStyles.None, out value);
            }
            finally
            {
                Return(rented);
            }
        }

        [SkipLocalsInit]
        public static bool TryDateOnlyExact(ReadOnlySpan<byte> column, string format, in FixedWidthContext context, out DateOnly value, byte trimChar = (byte)' ', TrimMode trimMode = TrimMode.Trailing)
        {
            Span<char> stack = stackalloc char[StackBufferSize];
            var text = Decode(TrimColumn(column, trimChar, trimMode), context.Encoding, stack, out char[]? rented);
            try
            {
                return DateOnly.TryParseExact(text, format, context.FormatProvider, DateTimeStyles.None, out value);
            }
            finally
            {
                Return(rented);
            }
        }

        [SkipLocalsInit]
        public static bool TryTimeOnlyExact(ReadOnlySpan<byte> column, string format, in FixedWidthContext context, out TimeOnly value, byte trimChar = (byte)' ', TrimMode trimMode = TrimMode.Trailing)
        {
            Span<char> stack = stackalloc char[StackBufferSize];
            var text = Decode(TrimColumn(column, trimChar, trimMode), context.Encoding, stack, out char[]? rented);
            try
            {
                return TimeOnly.TryParseExact(text, format, context.FormatProvider, DateTimeStyles.None, out value);
            }
            finally
            {
                Return(rented);
            }
        }

        [SkipLocalsInit]
        public static bool TryDateTimeOffsetExact(ReadOnlySpan<byte> column, string format, in FixedWidthContext context, out DateTimeOffset value, byte trimChar = (byte)' ', TrimMode trimMode = TrimMode.Trailing)
        {
            Span<char> stack = stackalloc char[StackBufferSize];
            var text = Decode(TrimColumn(column, trimChar, trimMode), context.Encoding, stack, out char[]? rented);
            try
            {
                return DateTimeOffset.TryParseExact(text, format, context.FormatProvider, DateTimeStyles.None, out value);
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
            bool ascii = System.Text.Ascii.IsValid(text);
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
            System.Text.Ascii.FromUtf16(text, bytes, out int written);
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
