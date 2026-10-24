using System.Text;
using BenchmarkDotNet.Attributes;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser;
using FixedWidthParser.Readers;

namespace Benchmarks.Perf
{
    /// <summary>
    /// Asynchronous reading from an in-memory stream: <c>await foreach</c> over the readers against a
    /// baseline of <see cref="StreamReader.ReadLineAsync()"/> plus parsing the re-encoded line.
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class AsyncReaderBenchmarks
    {
        private static readonly FixedWidthLayout<SampleModel> _layout = FixedWidthLayout.FromAttributes<SampleModel>();
        private readonly FixedWidthReaderOptions _pooledReaderOptions = new FixedWidthReaderOptions { StringPool = new StringPool() };
        private readonly FixedWidthReaderOptions _generatedPooled = new() { StringPool = new StringPool() };
        private byte[] _bytes = [];

        [Params(100, 1000)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _bytes = SampleData.Bytes(Count);
        }

        [Benchmark(Baseline = true)]
        public async Task<int> Naive_ReadLineAsync()
        {
            using var reader = new StreamReader(new MemoryStream(_bytes, writable: false), Encoding.UTF8);
            int sum = 0;
            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                if (FixedWidth.TryParse(Encoding.UTF8.GetBytes(line), _layout, default, out var model))
                {
                    sum += model.Age;
                }
            }
            return sum;
        }

        [Benchmark]
        public async Task<int> Reader_ReadAsync()
        {
            int sum = 0;
            await foreach (var model in FixedWidth.ReadAsync(new MemoryStream(_bytes, writable: false), _layout).ConfigureAwait(false))
            {
                sum += model.Age;
            }
            return sum;
        }

        [Benchmark]
        public async Task<int> Reader_ReadAsync_Pooled()
        {
            int sum = 0;
            await foreach (var model in FixedWidth.ReadAsync(new MemoryStream(_bytes, writable: false), _layout, options: _pooledReaderOptions).ConfigureAwait(false))
            {
                sum += model.Age;
            }
            return sum;
        }

        [Benchmark]
        public async Task<int> GeneratedReader_ReadAsync()
        {
            int sum = 0;
            await foreach (var model in FixedWidth.ReadAsync<GenSampleModel>(new MemoryStream(_bytes, writable: false)).ConfigureAwait(false))
            {
                sum += model.Age;
            }
            return sum;
        }

        [Benchmark]
        public async Task<int> GeneratedReader_ReadAsync_Pooled()
        {
            int sum = 0;
            await foreach (var model in FixedWidth.ReadAsync<GenSampleModel>(new MemoryStream(_bytes, writable: false), options: _generatedPooled).ConfigureAwait(false))
            {
                sum += model.Age;
            }
            return sum;
        }
    }
}
