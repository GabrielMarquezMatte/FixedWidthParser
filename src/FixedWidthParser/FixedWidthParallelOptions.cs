namespace FixedWidthParser
{
    /// <summary>Options for reading a file across several threads with <c>AggregateFileAsync</c>.</summary>
    public sealed class FixedWidthParallelOptions
    {
        internal static readonly FixedWidthParallelOptions Default = new();

        /// <summary>
        /// The maximum number of reading threads. <c>0</c>, the default, means
        /// <see cref="Environment.ProcessorCount"/>; <c>1</c> reads the file as one partition.
        /// </summary>
        public int DegreeOfParallelism { get; init; }

        // Bytes per partition; 0 sizes partitions from the file length. Tests set it to cut every boundary.
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

        internal static FixedWidthParallelOptions Validate(FixedWidthParallelOptions? options)
        {
            options ??= Default;
            if (options.DegreeOfParallelism < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.DegreeOfParallelism, "DegreeOfParallelism must not be negative.");
            }
            return options;
        }
    }
}
