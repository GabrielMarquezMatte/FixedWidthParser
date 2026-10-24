using System.Text;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser
{
    /// <summary>
    /// Settings for the <see cref="FixedWidth"/> parse, read and aggregate methods. Every property has a default, so <see langword="null"/>
    /// options and <c>new FixedWidthReaderOptions()</c> read the same way.
    /// </summary>
    public sealed class FixedWidthReaderOptions
    {
        internal static readonly FixedWidthReaderOptions Default = new();

        /// <summary>The format provider for numbers and dates. <see langword="null"/> means the invariant culture.</summary>
        public IFormatProvider? FormatProvider { get; init; }

        /// <summary>
        /// The encoding of the file. Must be ASCII-compatible, since column positions are bytes.
        /// <see langword="null"/> means UTF-8, and only then is a leading byte order mark skipped.
        /// </summary>
        public Encoding? Encoding { get; init; }

        /// <summary>Interns string columns when set; otherwise each string column is a new string.</summary>
        public StringPool? StringPool { get; init; }

        /// <summary>The number of bytes read from the source at a time. Defaults to 65,536.</summary>
        public int BufferSize { get; init; } = 65536;

        /// <summary>
        /// The longest line accepted, in bytes, without its line break. A longer line throws
        /// <see cref="InvalidDataException"/>, which bounds the memory a source without line breaks can
        /// make the reader buffer. Defaults to 1,048,576.
        /// </summary>
        public int MaxLineLength { get; init; } = 1024 * 1024;

        /// <summary>
        /// Called when a line fails to parse. Return <see langword="true"/> to skip the line and keep
        /// reading, or <see langword="false"/> to throw the exception. When <see langword="null"/>, the
        /// reader throws.
        /// </summary>
        public Func<FixedWidthParseException, bool>? OnParseError { get; init; }

        /// <summary>
        /// The maximum number of threads <c>AggregateFileAsync</c> and <c>AggregateAsync</c> read with.
        /// <c>0</c>, the default, means <see cref="Environment.ProcessorCount"/>; <c>1</c> reads in one pass.
        /// Other reads ignore it.
        /// </summary>
        public int DegreeOfParallelism { get; init; }

        // Bytes per partition or chunk; 0 sizes them from the source. Tests set it to cut every boundary.
        internal int ChunkSize { get; init; }

        internal int Workers
        {
            get
            {
                if (DegreeOfParallelism == 0)
                {
                    return Environment.ProcessorCount;
                }
                return DegreeOfParallelism;
            }
        }

        internal FixedWidthContext Context => new() { FormatProvider = FormatProvider!, Encoding = Encoding!, StringPool = StringPool };

        internal bool SkipsByteOrderMark => Encoding is null || Encoding.CodePage == Encoding.UTF8.CodePage;

        internal static FixedWidthReaderOptions Validate(FixedWidthReaderOptions? options)
        {
            options ??= Default;
            if (options.BufferSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.BufferSize, "BufferSize must be at least 1.");
            }
            if (options.MaxLineLength < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.MaxLineLength, "MaxLineLength must be at least 1.");
            }
            if (options.DegreeOfParallelism < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.DegreeOfParallelism, "DegreeOfParallelism must not be negative.");
            }
            return options;
        }
    }
}
