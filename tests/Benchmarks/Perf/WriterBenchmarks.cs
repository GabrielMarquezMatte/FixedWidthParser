using System.Globalization;
using BenchmarkDotNet.Attributes;
using FixedWidthParser.Writers;

namespace Benchmarks.Perf
{
    /// <summary>
    /// Measures the hot path of fixed-width writing. Writes to Stream.Null to isolate formatting +
    /// encoding from the cost of disk I/O. WriteMany_NewStream is the baseline.
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class WriterBenchmarks
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private readonly FixedWidthWriter<SampleModel> _writer = new();
        private StreamWriter _sink = null!;
        private SampleModel[] _models = [];

        [Params(1, 100, 1000)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _sink = new StreamWriter(Stream.Null);
            _models = new SampleModel[Count];
            for (int i = 0; i < Count; i++)
            {
                _models[i] = new SampleModel
                {
                    Name = "Name" + i,
                    Age = 20 + (i % 50),
                    Salary = 1000.0 + i
                };
            }
        }

        [GlobalCleanup]
        public void Cleanup() => _sink.Dispose();

        /// <summary>Creates/disposes a StreamWriter per call (over the Stream). Baseline.</summary>
        [Benchmark(Baseline = true)]
        public void WriteMany_NewStream()
        {
            _writer.WriteMany(Stream.Null, _models, Culture);
        }

        /// <summary>Reuses an existing StreamWriter (new overload). Should eliminate the fixed allocation.</summary>
        [Benchmark]
        public void WriteMany_ReuseWriter()
        {
            _writer.WriteMany(_sink, _models, Culture);
        }

        /// <summary>Writer reuse + ReadOnlySpan: no StreamWriter per call and no enumerator. Should be zero-alloc.</summary>
        [Benchmark]
        public void WriteMany_ReuseWriterSpan()
        {
            _writer.WriteMany(_sink, _models.AsSpan(), Culture);
        }

        /// <summary>
        /// Async creating a StreamWriter per call (Stream overload). Mixes the cost of the new
        /// StreamWriter with the async state-machine overhead — comparable to NewStream.
        /// </summary>
        [Benchmark]
        public Task WriteMany_AsyncNewStream()
        {
            return _writer.WriteManyAsync(Stream.Null, _models, Culture);
        }

        /// <summary>
        /// Async reusing the StreamWriter (StreamWriter overload). Isolates the pure async
        /// overhead: compared to WriteMany_ReuseWriter, the difference is only the async.
        /// </summary>
        [Benchmark]
        public Task WriteMany_AsyncReuseWriter()
        {
            return _writer.WriteManyAsync(_sink, _models, Culture);
        }
    }
}
