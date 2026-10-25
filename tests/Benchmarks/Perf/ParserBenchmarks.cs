using System.Text;
using BenchmarkDotNet.Attributes;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser;

namespace Benchmarks.Perf
{
    /// <summary>
    /// Measures the hot path of parsing a single fixed-width line, with and without a StringPool.
    /// Parse_NoPool is the baseline; the Ratio column flags relative regressions.
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class ParserBenchmarks
    {
        private static readonly FixedWidthLayout<SampleModel> _layout = FixedWidthLayout.FromAttributes<SampleModel>();
        private FixedWidthContext _pooled;
        private byte[] _line = [];

        [Params(
            "John Doe   30   60000.00 ",
            "Jane Smith 28   55000.00 ")]
        public string Line { get; set; } = string.Empty;

        [GlobalSetup]
        public void Setup()
        {
            _line = Encoding.UTF8.GetBytes(Line);
            _pooled = new FixedWidthContext { StringPool = new StringPool() };
        }

        [Benchmark(Baseline = true)]
        public SampleModel Parse_NoPool()
        {
            FixedWidth.TryParse(_line, _layout, default, out var model);
            return model;
        }

        [Benchmark]
        public SampleModel Parse_WithStringPool()
        {
            FixedWidth.TryParse(_line, _layout, _pooled, out var model);
            return model;
        }
    }
}
