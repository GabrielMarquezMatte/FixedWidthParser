namespace FixedWidthParser
{
    /// <summary>
    /// How one column of a <see cref="FixedWidthLayout.Build{TModel}"/> layout is trimmed, parsed and
    /// written: the settings <see cref="FixedColumnAttribute"/> carries, with the same defaults.
    /// </summary>
    public sealed class FixedWidthColumnOptions
    {
        internal static readonly FixedWidthColumnOptions Default = new();

        /// <summary>Which side a written value is aligned to. Defaults to <see cref="Alignment.Left"/>.</summary>
        public Alignment Alignment { get; init; } = Alignment.Left;

        /// <summary>The ASCII character that fills a written column around its value. Defaults to a space.</summary>
        public char Padding { get; init; } = ' ';

        /// <summary>The ASCII character trimmed from a column before it is parsed, and written for a <see langword="null"/> value. Defaults to a space.</summary>
        public char TrimChar { get; init; } = ' ';

        /// <summary>Which ends of a column <see cref="TrimChar"/> is trimmed from. Defaults to <see cref="TrimMode.Trailing"/>.</summary>
        public TrimMode TrimMode { get; init; } = TrimMode.Trailing;

        /// <summary>The format for writing, and for parsing a date or time exactly. <see langword="null"/> means the type's default.</summary>
        public string? Format { get; init; }

        /// <summary>What happens when a written value is wider than its column. <see cref="OverflowBehavior.Default"/> truncates a string and throws for anything else.</summary>
        public OverflowBehavior Overflow { get; init; } = OverflowBehavior.Default;
    }
}
