using System.Globalization;
using BenchmarkDotNet.Attributes;
using Benchmarks.Attributes;
using Benchmarks.Parsers;
using Benchmarks.Writers;
using CommunityToolkit.HighPerformance.Buffers;

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
    [MemoryDiagnoser]
    public class FixedParserBenchmark
    {
        [Params("Name      25   50000.00  ", "John Doe   30   60000.00 ", "Jane Smith 28   55000.00 ")]
        public string Line { get; set; } = string.Empty;
        private static readonly FixedWidthParser<SampleModel> _parser = new();
        private static readonly StringPool? _stringPool = null;
        [Benchmark]
        public SampleModel ParseLine()
        {
            _parser.TryParse(Line, CultureInfo.InvariantCulture, _stringPool, out var model);
            return model;
        }
    }

    public static class Program
    {
        public static void Main(string[] args)
        {
        }
    }
}