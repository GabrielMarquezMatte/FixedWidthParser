namespace FixedWidthParser
{
    /// <summary>Alignment of the content within the column when writing.</summary>
    public enum Alignment
    {
        Left,
        Right
    }

    /// <summary>Trim mode when parsing.</summary>
    public enum TrimMode
    {
        Trailing,
        Leading,
        Both
    }

    /// <summary>What to do when the formatted value does not fit the column width.</summary>
    public enum OverflowBehavior
    {
        /// <summary>Resolved per type: string truncates, others (numeric, etc.) throw.</summary>
        Default,
        /// <summary>Keeps the characters on the alignment side and discards the excess.</summary>
        Truncate,
        /// <summary>Throws <see cref="InvalidOperationException"/> (avoids silent data loss).</summary>
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

        /// <summary>
        /// ASCII character trimmed from the column before it is parsed (e.g. '0' for a zero-padded numeric
        /// column), from the ends <see cref="TrimMode"/> picks, and written for a <see langword="null"/>
        /// value. Default: space.
        /// </summary>
        public char TrimChar { get; set; } = ' ';

        /// <summary>Trim mode for the TrimChar. Default: <see cref="TrimMode.Trailing"/>.</summary>
        public TrimMode TrimMode { get; set; } = TrimMode.Trailing;

        /// <summary>Format string passed to <see cref="ISpanFormattable"/> (e.g. "F2", "N0"). Ignored for string.</summary>
        public string? Format { get; set; }

        /// <summary>Overflow policy when writing. Default: <see cref="OverflowBehavior.Default"/>.</summary>
        public OverflowBehavior Overflow { get; set; } = OverflowBehavior.Default;

        /// <summary>
        /// A type implementing <see cref="IFixedWidthConverter{T}"/>, where <c>T</c> is this column's member
        /// type, used instead of the built-in parsing and formatting. One instance serves every line.
        /// </summary>
        public Type? Converter { get; set; }
    }
}
