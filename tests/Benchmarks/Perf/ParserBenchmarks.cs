using System.Globalization;
using BenchmarkDotNet.Attributes;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Parsers;

namespace Benchmarks.Perf
{
    /// <summary>
    /// Measures the hot path of parsing a single fixed-width line, with and without a StringPool.
    /// Parse_NoPool is the baseline; the Ratio column flags relative regressions.
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class ParserBenchmarks
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private readonly FixedWidthParser<SampleModel> _parser = new();
        private StringPool _stringPool = null!;

        [Params(
            "John Doe   30   60000.00 ",
            "Jane Smith 28   55000.00 ")]
        public string Line { get; set; } = string.Empty;

        [GlobalSetup]
        public void Setup()
        {
            _stringPool = new StringPool();
        }

        [Benchmark(Baseline = true)]
        public SampleModel Parse_NoPool()
        {
            _parser.TryParse(Line, Culture, null, out var model);
            return model;
        }

        [Benchmark]
        public SampleModel Parse_WithStringPool()
        {
            _parser.TryParse(Line, Culture, _stringPool, out var model);
            return model;
        }
    }
}
