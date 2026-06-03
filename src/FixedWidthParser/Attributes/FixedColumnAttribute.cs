namespace FixedWidthParser.Attributes
{
    /// <summary>Alinhamento do conteúdo dentro da coluna na escrita.</summary>
    public enum Alignment
    {
        Left,
        Right
    }

    /// <summary>O que fazer quando o valor formatado não cabe na largura da coluna.</summary>
    public enum OverflowBehavior
    {
        /// <summary>Resolve por tipo: string trunca, demais (numéricos etc.) lançam.</summary>
        Default,
        /// <summary>Mantém os caracteres do lado do alinhamento e descarta o excedente.</summary>
        Truncate,
        /// <summary>Lança <see cref="System.InvalidOperationException"/> (evita perda silenciosa).</summary>
        Throw
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class FixedColumnAttribute(int start, int length) : Attribute
    {
        public int Start { get; } = start;
        public int Length { get; } = length;

        /// <summary>Alinhamento na escrita. Padrão: <see cref="Alignment.Left"/>.</summary>
        public Alignment Alignment { get; set; } = Alignment.Left;

        /// <summary>Caractere de preenchimento na escrita (ex.: '0' para zero-padding). Padrão: espaço.</summary>
        public char Padding { get; set; } = ' ';

        /// <summary>Format string repassada ao <see cref="ISpanFormattable"/> (ex.: "F2", "N0"). Ignorada para string.</summary>
        public string? Format { get; set; }

        /// <summary>Política de overflow na escrita. Padrão: <see cref="OverflowBehavior.Default"/>.</summary>
        public OverflowBehavior Overflow { get; set; } = OverflowBehavior.Default;
    }
}
