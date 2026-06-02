using System.Globalization;
using BenchmarkDotNet.Attributes;
using Benchmarks.Parsers;
using CommunityToolkit.HighPerformance.Buffers;

namespace Benchmarks.Perf
{
    /// <summary>
    /// Mede o caminho quente de parsing de uma linha fixed-width, com e sem StringPool.
    /// Parse_NoPool é o baseline; a coluna Ratio acusa regressões relativas.
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
        public void Setup() => _stringPool = new StringPool();

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
