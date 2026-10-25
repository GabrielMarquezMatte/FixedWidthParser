using System.Text;
using BenchmarkDotNet.Attributes;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser;

namespace Benchmarks.Perf
{
    /// <summary>
    /// Synchronous reading from an in-memory stream, to isolate parsing and buffering from disk I/O.
    /// The baseline reads a string per line with a <see cref="StreamReader"/> and encodes it back to
    /// bytes before parsing: what a caller would do by hand.
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class ReaderBenchmarks
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
        public int Naive_ReadLine()
        {
            using var reader = new StreamReader(new MemoryStream(_bytes, writable: false), Encoding.UTF8);
            int sum = 0;
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (FixedWidth.TryParse(Encoding.UTF8.GetBytes(line), _layout, default, out var model))
                {
                    sum += model.Age;
                }
            }
            return sum;
        }

        [Benchmark]
        public int Reader_Read()
        {
            int sum = 0;
            foreach (var model in FixedWidth.Read(new MemoryStream(_bytes, writable: false), _layout))
            {
                sum += model.Age;
            }
            return sum;
        }

        [Benchmark]
        public int Reader_Read_Pooled()
        {
            int sum = 0;
            foreach (var model in FixedWidth.Read(new MemoryStream(_bytes, writable: false), _layout, options: _pooledReaderOptions))
            {
                sum += model.Age;
            }
            return sum;
        }

        [Benchmark]
        public int GeneratedReader_Read()
        {
            int sum = 0;
            foreach (var model in FixedWidth.Read<GenSampleModel>(new MemoryStream(_bytes, writable: false)))
            {
                sum += model.Age;
            }
            return sum;
        }

        [Benchmark]
        public int GeneratedReader_Read_Pooled()
        {
            int sum = 0;
            foreach (var model in FixedWidth.Read<GenSampleModel>(new MemoryStream(_bytes, writable: false), options: _generatedPooled))
            {
                sum += model.Age;
            }
            return sum;
        }
    }
}
