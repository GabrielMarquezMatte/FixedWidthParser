namespace FixedWidthParser.Attributes
{
    /// <summary>Alignment of the content within the column when writing.</summary>
    public enum Alignment
    {
        Left,
        Right
    }

    /// <summary>What to do when the formatted value does not fit the column width.</summary>
    public enum OverflowBehavior
    {
        /// <summary>Resolved per type: string truncates, others (numeric, etc.) throw.</summary>
        Default,
        /// <summary>Keeps the characters on the alignment side and discards the excess.</summary>
        Truncate,
        /// <summary>Throws <see cref="System.InvalidOperationException"/> (avoids silent data loss).</summary>
        Throw
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class FixedColumnAttribute(int start, int length) : Attribute
    {
        public int Start { get; } = start;
        public int Length { get; } = length;

        /// <summary>Alignment when writing. Default: <see cref="Alignment.Left"/>.</summary>
        public Alignment Alignment { get; set; } = Alignment.Left;

        /// <summary>Padding character when writing (e.g. '0' for zero-padding). Default: space.</summary>
        public char Padding { get; set; } = ' ';

        /// <summary>Format string passed to <see cref="ISpanFormattable"/> (e.g. "F2", "N0"). Ignored for string.</summary>
        public string? Format { get; set; }

        /// <summary>Overflow policy when writing. Default: <see cref="OverflowBehavior.Default"/>.</summary>
        public OverflowBehavior Overflow { get; set; } = OverflowBehavior.Default;
    }
}
