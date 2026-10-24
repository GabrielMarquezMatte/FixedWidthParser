using System.Text;

namespace FixedWidthParser
{
    /// <summary>Settings for the <see cref="FixedWidth"/> write methods. Every property has a default.</summary>
    public sealed class FixedWidthWriterOptions
    {
        internal static readonly FixedWidthWriterOptions Default = new();

        /// <summary>The format provider for numbers and dates. <see langword="null"/> means the invariant culture.</summary>
        public IFormatProvider? FormatProvider { get; init; }

        /// <summary>The encoding of the file. Must be ASCII-compatible, since column positions are bytes. <see langword="null"/> means UTF-8, written without a byte order mark.</summary>
        public Encoding? Encoding { get; init; }

        /// <summary>The line break written after each record. Defaults to <see cref="Environment.NewLine"/>.</summary>
        public string NewLine { get; init; } = Environment.NewLine;

        internal FixedWidthContext Context => new() { FormatProvider = FormatProvider!, Encoding = Encoding! };

        /// <summary><see cref="NewLine"/> in the file's encoding, encoded once per options instance.</summary>
        internal byte[] NewLineBytes => field ??= Context.Encoding.GetBytes(NewLine);

        internal static FixedWidthWriterOptions Validate(FixedWidthWriterOptions? options)
        {
            options ??= Default;
            if (options.NewLine is null)
            {
                throw new ArgumentException("NewLine must not be null.", nameof(options));
            }
            return options;
        }
    }
}
