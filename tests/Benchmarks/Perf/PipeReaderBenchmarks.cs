using System.Globalization;
using System.IO.Pipelines;
using BenchmarkDotNet.Attributes;
using FixedWidthParser.Readers;
using FixedWidthParser.Writers;

namespace Benchmarks.Perf
{
    /// <summary>
    /// Async byte reading from a <see cref="Stream"/> (the existing pooled <c>LineBufferState</c> core)
    /// versus a <see cref="PipeReader"/> (the pipe owns buffering/read-ahead). The source is an in-memory
    /// <see cref="MemoryStream"/>, rewound per invocation, so the comparison isolates buffering + line
    /// splitting + parsing from disk I/O. <see cref="Pipe_ReadAsync_SmallSegments"/> forces a tiny pipe
    /// buffer so most records straddle segment boundaries, exercising the pooled scratch-copy path.
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class PipeReaderBenchmarks
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private readonly FixedWidthByteReader<SampleModel> _byteReader = new(CultureInfo.InvariantCulture);
        private MemoryStream _stream = new();

        [Params(100, 1000)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            var models = new SampleModel[Count];
            for (int i = 0; i < Count; i++)
            {
                models[i] = new SampleModel { Name = "Name" + (i % 100), Age = 20 + (i % 50), Salary = 1000.0 + i };
            }

            var writer = new FixedWidthWriter<SampleModel>();
            _stream = new MemoryStream();
            writer.WriteMany(_stream, models.AsSpan(), Culture);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _stream.Dispose();
        }

        /// <summary>Stream async path: manual pooled buffer with compaction/growth. Baseline.</summary>
        [Benchmark(Baseline = true)]
        public async Task<int> Stream_ReadAsync()
        {
            _stream.Position = 0;
            int sum = 0;
            await foreach (var model in _byteReader.ReadAsync(_stream, leaveOpen: true).ConfigureAwait(false))
            {
                sum += model.Age;
            }

            return sum;
        }

        /// <summary>PipeReader path with default segment sizing: lines are typically contiguous (parsed in place).</summary>
        [Benchmark]
        public async Task<int> Pipe_ReadAsync()
        {
            _stream.Position = 0;
            // leaveOpen on the pipe options keeps the reused MemoryStream alive; the reader completes the pipe.
            var pipe = PipeReader.Create(_stream, new StreamPipeReaderOptions(leaveOpen: true));
            int sum = 0;
            await foreach (var model in _byteReader.ReadAsync(pipe).ConfigureAwait(false))
            {
                sum += model.Age;
            }

            return sum;
        }

        /// <summary>PipeReader path with a tiny buffer: most lines span segments → pooled scratch copy.</summary>
        [Benchmark]
        public async Task<int> Pipe_ReadAsync_SmallSegments()
        {
            _stream.Position = 0;
            var pipe = PipeReader.Create(_stream, new StreamPipeReaderOptions(bufferSize: 16, leaveOpen: true));
            int sum = 0;
            await foreach (var model in _byteReader.ReadAsync(pipe).ConfigureAwait(false))
            {
                sum += model.Age;
            }

            return sum;
        }
    }
}
