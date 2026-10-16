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

        /// <summary>The format provider for numbers and dates. Defaults to <see cref="CultureInfo.InvariantCulture"/>.</summary>
        public IFormatProvider FormatProvider
        {
            get => _formatProvider ?? CultureInfo.InvariantCulture;
            init
            {
                _formatProvider = value;
                DecimalSeparator = PlainDecimalSeparator(value);
            }
        }

        /// <summary>
        /// The provider's decimal separator as one ASCII byte when its negative sign is "-", so a plain
        /// number can be written without going through the provider; 0 otherwise.
        /// </summary>
        // ponytail: read once, so a NumberFormatInfo mutated after this context was built is not seen.
        internal byte DecimalSeparator { get => _formatProvider is null ? (byte)'.' : field; private init; }

        private static byte PlainDecimalSeparator(IFormatProvider? provider)
        {
            if (provider is null)
            {
                return (byte)'.';
            }
            var info = NumberFormatInfo.GetInstance(provider);
            if (!string.Equals(info.NegativeSign, "-", StringComparison.Ordinal) || info.NumberDecimalSeparator is not [var separator] || !char.IsAscii(separator))
            {
                return 0;
            }
            return (byte)separator;
        }

        /// <summary>
        /// The encoding of the text in the line, used to decode and encode string columns, dates parsed
        /// with a format, and types that only parse from characters. Must be ASCII-compatible (UTF-8,
        /// Latin-1, Windows-1252, …), since column positions are bytes. Defaults to UTF-8.
        /// </summary>
        public Encoding Encoding
        {
            get => field ?? Encoding.UTF8;
            init
            {
                field = value;
                IsUtf8 = value is null || value.CodePage == Encoding.UTF8.CodePage;
            }
        }

        /// <summary>True when <see cref="Encoding"/> is UTF-8, so UTF-8 bytes can be written as they are.</summary>
        // The backing field stores the negation, so default(FixedWidthContext), which means UTF-8, reads as UTF-8.
        internal bool IsUtf8 { get => !field; private init => field = !value; }

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
