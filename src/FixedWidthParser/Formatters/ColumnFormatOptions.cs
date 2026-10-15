using System.Text;
using FixedWidthParser.Attributes;

namespace FixedWidthParser.Formatters
{
    /// <summary>
    /// Resolved formatting options for a column (alignment, padding, format string and an overflow
    /// policy already resolved to <see cref="OverflowBehavior.Truncate"/> or
    /// <see cref="OverflowBehavior.Throw"/>). Centralizes placing the content into the line slice,
    /// shared by the generated and runtime writers. <see cref="Padding"/> must be ASCII.
    /// </summary>
    public readonly record struct ColumnFormatOptions(Alignment Alignment, char Padding, string? Format, OverflowBehavior Overflow)
    {
        /// <summary>
        /// Writes the encoded <paramref name="content"/> into the column slice <paramref name="slice"/>,
        /// applying alignment, padding and overflow.
        /// </summary>
        public void WriteInto(ReadOnlySpan<byte> content, Span<byte> slice, string columnName)
        {
            int width = slice.Length;
            int length = content.Length;
            byte padding = (byte)Padding;

            if (length <= width)
            {
                if (Alignment == Alignment.Right)
                {
                    int pad = width - length;
                    if (Padding != ' ' && content.Length > 0 && (content[0] == (byte)'-' || content[0] == (byte)'+'))
                    {
                        slice[0] = content[0];
                        slice[1..(pad + 1)].Fill(padding);
                        content[1..].CopyTo(slice[(pad + 1)..]);
                    }
                    else
                    {
                        slice[..pad].Fill(padding);
                        content.CopyTo(slice[pad..]);
                    }
                }
                else
                {
                    content.CopyTo(slice);
                    slice[length..].Fill(padding);
                }
                return;
            }

            if (Overflow == OverflowBehavior.Throw)
            {
                throw new InvalidOperationException(
                    $"Value \"{Encoding.UTF8.GetString(content)}\" ({length} bytes) exceeds the width {width} of column \"{columnName}\".");
            }

            // Truncate: keep the bytes on the alignment side.
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
