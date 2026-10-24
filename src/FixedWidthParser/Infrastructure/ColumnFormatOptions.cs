using System.ComponentModel;
using System.Text;

namespace FixedWidthParser.Infrastructure
{
    /// <summary>
    /// Resolved formatting options for a column (alignment, padding, format string and an overflow
    /// policy already resolved to <see cref="OverflowBehavior.Truncate"/> or
    /// <see cref="OverflowBehavior.Throw"/>), passed by generated code and layouts to
    /// <see cref="FixedWidthRuntime"/>. <see cref="Padding"/> must be ASCII.
    /// <para>Public because generated code constructs it; not intended for direct use.</para>
    /// </summary>
    /// <remarks>
    /// Generated code in consumer assemblies calls the constructor, so a new setting arrives as a new
    /// constructor overload and a new property; the existing constructor is never changed.
    /// </remarks>
#pragma warning disable CA1815 // Never compared; value equality is meaningless for it.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct ColumnFormatOptions
#pragma warning restore CA1815
    {
        /// <summary>Creates the options for one column.</summary>
        public ColumnFormatOptions(Alignment alignment, char padding, string? format, OverflowBehavior overflow)
        {
            Alignment = alignment;
            Padding = padding;
            Format = format;
            Overflow = overflow;
            FastDate = FastDate.TryCreate(format);
        }

        /// <summary>Which side a written value is aligned to.</summary>
        public Alignment Alignment { get; }

        /// <summary>The ASCII character that fills the column around its value.</summary>
        public char Padding { get; }

        /// <summary>The format string for the value, or <see langword="null"/> for the type's default.</summary>
        public string? Format { get; }

        /// <summary>The resolved overflow policy: <see cref="OverflowBehavior.Truncate"/> or <see cref="OverflowBehavior.Throw"/>.</summary>
        public OverflowBehavior Overflow { get; }

        /// <summary><see cref="Format"/> compiled for <see cref="Infrastructure.FastDate"/>, or <see langword="null"/> when it cannot be.</summary>
        internal FastDate? FastDate { get; }

        /// <summary>
        /// Writes the encoded <paramref name="content"/> into the column slice <paramref name="slice"/>,
        /// applying alignment, padding and overflow.
        /// </summary>
        internal void WriteInto(ReadOnlySpan<byte> content, Span<byte> slice, string columnName)
        {
            int width = slice.Length;
            int length = content.Length;
            if (length > width)
            {
                if (Overflow == OverflowBehavior.Throw)
                {
                    throw new InvalidOperationException(
                        $"Value \"{Encoding.UTF8.GetString(content)}\" ({length} bytes) exceeds the width {width} of column \"{columnName}\".");
                }
                (Alignment == Alignment.Right ? content[(length - width)..] : content[..width]).CopyTo(slice);
                return;
            }
            content.CopyTo(slice);
            AlignInPlace(slice, length);
        }

        /// <summary>
        /// Aligns and pads <paramref name="length"/> bytes already formatted at the start of
        /// <paramref name="slice"/>, exactly as <see cref="WriteInto"/> would place them.
        /// </summary>
        internal void AlignInPlace(Span<byte> slice, int length)
        {
            byte padding = (byte)Padding;
            int pad = slice.Length - length;
            if (Alignment != Alignment.Right)
            {
                slice[length..].Fill(padding);
                return;
            }
            if (pad == 0)
            {
                return;
            }
            byte first = slice[0];
            slice[..length].CopyTo(slice[pad..]);
            if (Padding != ' ' && length > 0 && (first == (byte)'-' || first == (byte)'+'))
            {
                // The sign stays leftmost, ahead of the padding.
                slice[..(pad + 1)].Fill(padding);
                slice[0] = first;
                return;
            }
            slice[..pad].Fill(padding);
        }
    }
}
