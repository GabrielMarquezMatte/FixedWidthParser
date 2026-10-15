using System.IO.Pipelines;
using BenchmarkDotNet.Attributes;
using FixedWidthParser.Readers;

namespace Benchmarks.Perf
{
    /// <summary>
    /// Reading the same bytes through a <see cref="Stream"/> (the reader owns buffering) versus a
    /// <see cref="PipeReader"/> (the pipe owns buffering/read-ahead). The source is in memory, so the
    /// comparison isolates buffering and line splitting from I/O.
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class PipeReaderBenchmarks
    {
        private readonly FixedWidthReader<SampleModel> _reader = new();
        private byte[] _bytes = [];

        [Params(1000)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _bytes = SampleData.Bytes(Count);
        }

        [Benchmark(Baseline = true)]
        public async Task<int> Stream_ReadAsync()
        {
            int sum = 0;
            await foreach (var model in _reader.ReadAsync(new MemoryStream(_bytes, writable: false)).ConfigureAwait(false))
            {
                sum += model.Age;
            }
            return sum;
        }

        /// <summary>Default segment sizing: lines are typically contiguous (parsed in place).</summary>
        [Benchmark]
        public async Task<int> Pipe_ReadAsync()
        {
            int sum = 0;
            await foreach (var model in _reader.ReadAsync(PipeReader.Create(new MemoryStream(_bytes, writable: false))).ConfigureAwait(false))
            {
                sum += model.Age;
            }
            return sum;
        }

        /// <summary>A tiny buffer: most lines span segments, so they go through the pooled scratch copy.</summary>
        [Benchmark]
        public async Task<int> Pipe_ReadAsync_SmallSegments()
        {
            int sum = 0;
            var pipe = PipeReader.Create(new MemoryStream(_bytes, writable: false), new StreamPipeReaderOptions(bufferSize: 16));
            await foreach (var model in _reader.ReadAsync(pipe).ConfigureAwait(false))
            {
                sum += model.Age;
            }
            return sum;
        }
    }
}
