using FixedWidthParser.Attributes;

namespace FixedWidthParser.Formatters
{
    /// <summary>
    /// Opções de formatação resolvidas de uma coluna (alinhamento, preenchimento, format string e
    /// política de overflow já resolvida para <see cref="OverflowBehavior.Truncate"/> ou
    /// <see cref="OverflowBehavior.Throw"/>). Centraliza a colocação do conteúdo na fatia da linha,
    /// compartilhada por todos os formatters.
    /// </summary>
    public readonly struct ColumnFormatOptions(Alignment alignment, char padding, string? format, OverflowBehavior overflow)
    {
        public Alignment Alignment { get; } = alignment;
        public char Padding { get; } = padding;
        public string? Format { get; } = format;
        public OverflowBehavior Overflow { get; } = overflow;

        /// <summary>
        /// Escreve <paramref name="content"/> na fatia <paramref name="slice"/> da coluna,
        /// aplicando alinhamento, preenchimento e overflow.
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
                    slice[..pad].Fill(Padding);
                    content.CopyTo(slice[pad..]);
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
                    $"Valor \"{content}\" ({length} caracteres) excede a largura {width} da coluna \"{columnName}\".");
            }

            // Truncate: mantém os caracteres do lado do alinhamento.
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
