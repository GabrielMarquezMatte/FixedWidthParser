using FixedWidthParser.Attributes;

namespace FixedWidthParser.Formatters
{
    /// <summary>
    /// Resolved formatting options for a column (alignment, padding, format string and an overflow
    /// policy already resolved to <see cref="OverflowBehavior.Truncate"/> or
    /// <see cref="OverflowBehavior.Throw"/>). Centralizes placing the content into the line slice,
    /// shared by all formatters.
    /// </summary>
    public readonly record struct ColumnFormatOptions(Alignment Alignment, char Padding, string? Format, OverflowBehavior Overflow)
    {
        /// <summary>
        /// Writes <paramref name="content"/> into the column slice <paramref name="slice"/>,
        /// applying alignment, padding and overflow.
        /// </summary>
        public void WriteInto(ReadOnlySpan<char> content, Span<char> slice, string columnName)
        {
            int width = slice.Length;
            int length = content.Length;

            if (length <= width)
            {
                if (Alignment == Alignment.Right)
                {
                    int pad = width - length;
                    if (Padding != ' ' && content.Length > 0 && (content[0] == '-' || content[0] == '+'))
                    {
                        slice[0] = content[0];
                        slice[1..(pad + 1)].Fill(Padding);
                        content[1..].CopyTo(slice[(pad + 1)..]);
                    }
                    else
                    {
                        slice[..pad].Fill(Padding);
                        content.CopyTo(slice[pad..]);
                    }
                }
                else
                {
                    content.CopyTo(slice);
                    slice[length..].Fill(Padding);
                }
                return;
            }

            if (Overflow == OverflowBehavior.Throw)
            {
                throw new InvalidOperationException(
                    $"Value \"{content}\" ({length} chars) exceeds the width {width} of column \"{columnName}\".");
            }

            // Truncate: keep the characters on the alignment side.
            if (Alignment == Alignment.Right)
            {
                content[(length - width)..].CopyTo(slice);
            }
            else
            {
                content[..width].CopyTo(slice);
            }
        }
    }
}
