using System.Text;

namespace FixedWidthParser
{
    /// <summary>Settings for <see cref="Writers.FixedWidthWriter{TModel}"/>. Every property has a default.</summary>
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
    }
}
