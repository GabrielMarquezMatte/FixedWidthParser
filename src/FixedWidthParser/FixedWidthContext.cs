using System.Globalization;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Readers;

namespace FixedWidthParser
{
    /// <summary>
    /// Settings a model parser, model writer or converter needs for one line, handed to every
    /// generated <c>TryParse</c>/<c>TryFormat</c> and every <see cref="Processors.IFixedWidthConverter{T}"/> call.
    /// </summary>
    /// <remarks>
    /// Passed by <see langword="in"/> reference. New settings arrive as new properties, so a converter
    /// written against this type keeps compiling. <see langword="default"/> means the invariant culture,
    /// UTF-8 and no string pool; construct one with an object initializer to unit-test a converter.
    /// </remarks>
    public readonly record struct FixedWidthContext
    {
        private readonly IFormatProvider? _formatProvider;
        private readonly Encoding? _encoding;

        /// <summary>The format provider for numbers and dates. Defaults to <see cref="CultureInfo.InvariantCulture"/>.</summary>
        public IFormatProvider FormatProvider
        {
            get => _formatProvider ?? CultureInfo.InvariantCulture;
            init => _formatProvider = value;
        }

        /// <summary>
        /// The encoding of the text in the line, used to decode and encode string columns, dates parsed
        /// with a format, and types that only parse from characters. Must be ASCII-compatible (UTF-8,
        /// Latin-1, Windows-1252, …), since column positions are bytes. Defaults to UTF-8.
        /// </summary>
        public Encoding Encoding
        {
            get => _encoding ?? Encoding.UTF8;
            init => _encoding = value;
        }

        /// <summary>Interns string columns when set; otherwise each string column is a new string.</summary>
        public StringPool? StringPool { get; init; }

        internal ParseFailure? Failure { get; init; }

        /// <summary>
        /// Records that <paramref name="columnName"/> rejected the line, so a reader can name it in its
        /// <see cref="FixedWidthParseException"/>. Generated parsers call this before returning
        /// <see langword="false"/>; outside a reader's diagnosis it does nothing.
        /// </summary>
        public void ReportFailedColumn(string columnName, int start, int length)
        {
            Failure?.SetColumn(columnName, start, length);
        }

        /// <summary>
        /// Records that the line is shorter than the <paramref name="requiredLength"/> the layout needs.
        /// Generated parsers call this before returning <see langword="false"/>; outside a reader's
        /// diagnosis it does nothing.
        /// </summary>
        public void ReportShortLine(int requiredLength)
        {
            Failure?.SetShortLine(requiredLength);
        }
    }
}
