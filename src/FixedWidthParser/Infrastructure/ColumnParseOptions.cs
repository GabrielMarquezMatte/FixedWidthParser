using System.ComponentModel;

namespace FixedWidthParser.Infrastructure
{
    /// <summary>
    /// How a column is trimmed and, for a date or time, the exact format it is parsed with, passed by
    /// generated code and layouts to <see cref="FixedWidthRuntime"/>. <see cref="TrimChar"/> must be ASCII.
    /// <para>Public because generated code constructs it; not intended for direct use.</para>
    /// </summary>
    /// <remarks>
    /// Generated code in consumer assemblies calls the constructor, so a new setting arrives as a new
    /// constructor overload and a new property; the existing constructor is never changed.
    /// </remarks>
#pragma warning disable CA1815 // Never compared; value equality is meaningless for it.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct ColumnParseOptions
#pragma warning restore CA1815
    {
        /// <summary>Creates the options for one column.</summary>
        public ColumnParseOptions(char trimChar, TrimMode trimMode, string? format)
        {
            TrimChar = trimChar;
            TrimMode = trimMode;
            Format = format;
            FastDate = FastDate.TryCreate(format);
        }

        /// <summary>The ASCII character trimmed from the column before it is parsed, and that a <see langword="null"/> value is written with.</summary>
        public char TrimChar { get; }

        /// <summary>Which ends of the column <see cref="TrimChar"/> is trimmed from.</summary>
        public TrimMode TrimMode { get; }

        /// <summary>The exact format of a date or time column, or <see langword="null"/>.</summary>
        public string? Format { get; }

        internal byte TrimByte => (byte)TrimChar;

        /// <summary><see cref="Format"/> compiled for <see cref="Infrastructure.FastDate"/>, or <see langword="null"/> when it cannot be.</summary>
        internal FastDate? FastDate { get; }
    }
}
