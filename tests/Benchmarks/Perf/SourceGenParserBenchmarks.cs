using System.Globalization;
using BenchmarkDotNet.Attributes;
using FixedWidthParser;
using FixedWidthParser.Attributes;
using FixedWidthParser.Parsers;

namespace Benchmarks.Perf
{
    /// <summary>Same layout as <see cref="SampleModel"/>, but opts into the source-generated parser.</summary>
    public readonly partial record struct GenSampleModel : IFixedWidthModel<GenSampleModel>
    {
        [FixedColumn(0, 10)] public string Name { get; init; }
        [FixedColumn(10, 5)] public int Age { get; init; }
        [FixedColumn(15, 10)] public double Salary { get; init; }
    }

    /// <summary>
    /// Compares the reflection-based runtime parser (baseline) against the source-generated,
    /// reflection-free parser for an identical line and layout.
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class SourceGenParserBenchmarks
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private readonly FixedWidthParser<SampleModel> _reflection = new();

        [Params("John Doe  30   6000000.00")]
        public string Line { get; set; } = string.Empty;

        [Benchmark(Baseline = true)]
        public int Reflection()
        {
            _reflection.TryParse(Line, Culture, null, out var model);
            return model.Age;
        }

        [Benchmark]
        public int Generated()
        {
            FixedWidth.TryParse<GenSampleModel>(Line, Culture, null, out var model);
            return model.Age;
        }
    }
}
