namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Where the parallel reader puts each line of a partition. A <see langword="struct"/>, so the reader is
    /// specialized per sink and the per-line call devirtualizes.
    /// </summary>
    internal interface ILineSink<TSelf> where TSelf : struct, ILineSink<TSelf>
    {
        /// <summary>Takes one line; <see langword="false"/> when it does not parse, leaving the sink as it was.</summary>
        bool TryAdd(ReadOnlySpan<byte> line, in FixedWidthContext context);

        /// <summary>The exception for a line <see cref="TryAdd"/> rejected.</summary>
        FixedWidthParseException Diagnose(ReadOnlySpan<byte> line, long lineNumber, in FixedWidthContext context);

        /// <summary>Folds the sink of the partition that follows this one into it.</summary>
        void Merge(TSelf following);
    }
}
