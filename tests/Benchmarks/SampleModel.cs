using FixedWidthParser.Attributes;

namespace Benchmarks
{
    public readonly record struct SampleModel
    {
        public SampleModel()
        {
            Name = string.Empty;
            Age = 0;
            Salary = 0.0;
        }
        [FixedColumn(0, 10)]
        public string Name { get; init; } = string.Empty;
        [FixedColumn(10, 5)]
        public int Age { get; init; }
        [FixedColumn(15, 10)]
        public double Salary { get; init; }
    }

    /// <summary>
    /// Same numeric columns as <see cref="SampleModel"/> but with <b>no string column</b>. Used to
    /// isolate the per-line string allocation: a model without strings has nothing to decode or pool,
    /// so the reader allocates (essentially) nothing per line regardless of the StringPool.
    /// </summary>
    public readonly record struct NumericModel
    {
        public NumericModel()
        {
            Age = 0;
            Salary = 0.0;
        }
        [FixedColumn(0, 5)]
        public int Age { get; init; }
        [FixedColumn(5, 10)]
        public double Salary { get; init; }
    }
}
