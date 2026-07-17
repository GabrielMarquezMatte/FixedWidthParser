namespace FixedWidthParser.Attributes
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
        /// Character trimmed from the end of the column when parsing (e.g. '0' for a zero-padded
        /// numeric column written by another producer). Ignored for <c>string</c> columns' interning
        /// identity in the same way as the default — it only changes which character is stripped
        /// before the value is handed to the parser/converter. Default: space.
        /// </summary>
        public char TrimChar { get; set; } = ' ';

        /// <summary>Trim mode for the TrimChar. Default: <see cref="TrimMode.Trailing"/>.</summary>
        public TrimMode TrimMode { get; set; } = TrimMode.Trailing;

        /// <summary>Format string passed to <see cref="ISpanFormattable"/> (e.g. "F2", "N0"). Ignored for string.</summary>
        public string? Format { get; set; }

        /// <summary>Overflow policy when writing. Default: <see cref="OverflowBehavior.Default"/>.</summary>
        public OverflowBehavior Overflow { get; set; } = OverflowBehavior.Default;

        /// <summary>
        /// A type implementing <c>IFixedWidthConverter&lt;T&gt;</c> (char parser/writer) and/or
        /// <c>IUtf8FixedWidthConverter&lt;T&gt;</c> (UTF-8 parser/writer), where <c>T</c> is this
        /// column's member type. When set, it is used instead of the built-in <c>ISpanParsable</c>
        /// fallback, for both parsing and writing. A single instance is created once per model type.
        /// </summary>
        public Type? Converter { get; set; }
    }
}
