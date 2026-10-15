using System.Diagnostics.CodeAnalysis;

namespace FixedWidthParser
{
    /// <summary>
    /// Thrown by a reader when a line does not parse into the model, or handed to
    /// <see cref="FixedWidthReaderOptions.OnParseError"/>. Derives from <see cref="FormatException"/>, so
    /// existing <c>catch (FormatException)</c> blocks still catch it.
    /// </summary>
    public sealed class FixedWidthParseException : FormatException
    {
        /// <summary>Creates an exception with no message.</summary>
        [ExcludeFromCodeCoverage]
        public FixedWidthParseException()
        {
        }

        /// <summary>Creates an exception with the given message.</summary>
        [ExcludeFromCodeCoverage]
        public FixedWidthParseException(string message)
            : base(message)
        {
        }

        /// <summary>Creates an exception with the given message and inner exception.</summary>
        [ExcludeFromCodeCoverage]
        public FixedWidthParseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        internal FixedWidthParseException(string message, long lineNumber, string? columnName, int columnStart, int columnLength, string? rawValue)
            : base(message)
        {
            LineNumber = lineNumber;
            ColumnName = columnName;
            ColumnStart = columnStart;
            ColumnLength = columnLength;
            RawValue = rawValue;
        }

        /// <summary>The 1-based number of the line that failed. Empty lines count.</summary>
        public long LineNumber { get; }

        /// <summary>
        /// The column that rejected the line, or <see langword="null"/> when the line is shorter than the
        /// layout or the model's parser did not report a column.
        /// </summary>
        public string? ColumnName { get; }

        /// <summary>The 0-based start of <see cref="ColumnName"/>, or -1 when there is no column.</summary>
        public int ColumnStart { get; } = -1;

        /// <summary>The width of <see cref="ColumnName"/>, or 0 when there is no column.</summary>
        public int ColumnLength { get; }

        /// <summary>The untrimmed text of <see cref="ColumnName"/>, or <see langword="null"/> when there is no column.</summary>
        public string? RawValue { get; }
    }
}
