using FixedWidthParser;
using BenchmarkDotNet.Attributes;

namespace Benchmarks.Perf
{
    /// <summary>
    /// Measures the hot path of fixed-width writing. Writes to Stream.Null to isolate formatting from
    /// the cost of disk I/O. WriteMany_Enumerable is the baseline.
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class WriterBenchmarks
    {
        private static readonly FixedWidthLayout<SampleModel> _layout = FixedWidthLayout.FromAttributes<SampleModel>();
        private SampleModel[] _models = [];

        [Params(1, 100, 1000)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _models = SampleData.Models(Count);
        }

        [Benchmark(Baseline = true)]
        public void WriteMany_Enumerable()
        {
            FixedWidth.WriteMany(Stream.Null, (IEnumerable<SampleModel>)_models, _layout);
        }

        /// <summary>ReadOnlySpan: no enumerator, each record read by reference. Should be zero-alloc.</summary>
        [Benchmark]
        public void WriteMany_Span()
        {
            FixedWidth.WriteMany(Stream.Null, _models.AsSpan(), _layout);
        }

        /// <summary>The async overload: the difference from WriteMany_Enumerable is the async state machine.</summary>
        [Benchmark]
        public Task WriteManyAsync()
        {
            return FixedWidth.WriteManyAsync(Stream.Null, _models, _layout);
        }
    }
}
