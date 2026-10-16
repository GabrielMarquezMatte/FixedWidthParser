namespace FixedWidthParser
{
    /// <summary>
    /// Folds the records of a file read by <c>AggregateFileAsync</c> into a result. Each partition of the
    /// file gets its own instance.
    /// </summary>
    /// <typeparam name="TSelf">The implementing type.</typeparam>
    /// <typeparam name="TModel">The record type.</typeparam>
    /// <remarks>
    /// <para>
    /// <see cref="Add"/> runs concurrently across instances on worker threads, and must mutate only the
    /// instance it is called on. Every line is parsed and added exactly once, by the partition its first
    /// byte falls in.
    /// </para>
    /// <para>
    /// <see cref="Merge"/> runs on a single thread, left to right in file order, folding each later
    /// partition into the accumulator of everything before it. It may receive an instance that saw no
    /// records. A file read as a single partition never calls it.
    /// </para>
    /// </remarks>
    public interface IRecordAccumulator<TSelf, TModel>
        where TSelf : IRecordAccumulator<TSelf, TModel>
    {
        /// <summary>Folds one record into this instance.</summary>
        void Add(in TModel model);

        /// <summary>Folds the records of the partition that immediately follows this one into this instance.</summary>
        /// <param name="following">The accumulator of the following partition. It is not used again afterwards.</param>
        void Merge(TSelf following);
    }
}
