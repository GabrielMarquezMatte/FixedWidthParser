using BenchmarkDotNet.Attributes;
using FixedWidthParser.Writers;

namespace Benchmarks.Perf
{
    /// <summary>
    /// Measures the hot path of fixed-width writing. Writes to Stream.Null to isolate formatting from
    /// the cost of disk I/O. WriteMany_Enumerable is the baseline.
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class WriterBenchmarks
    {
        private readonly FixedWidthWriter<SampleModel> _writer = new();
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
            _writer.WriteMany(Stream.Null, (IEnumerable<SampleModel>)_models);
        }

        /// <summary>ReadOnlySpan: no enumerator, each record read by reference. Should be zero-alloc.</summary>
        [Benchmark]
        public void WriteMany_Span()
        {
            _writer.WriteMany(Stream.Null, _models.AsSpan());
        }

        /// <summary>The async overload: the difference from WriteMany_Enumerable is the async state machine.</summary>
        [Benchmark]
        public Task WriteManyAsync()
        {
            return _writer.WriteManyAsync(Stream.Null, _models);
        }
    }
}
