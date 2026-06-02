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
}
