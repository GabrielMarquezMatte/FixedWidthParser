using BenchmarkDotNet.Attributes;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser;
using FixedWidthParser.Readers;
using FixedWidthParser.Writers;

namespace Benchmarks.Perf
{
    /// <summary>
    /// Where the reader's per-line allocation comes from — the string column — and what a
    /// <see cref="StringPool"/> saves. The pool is reused across invocations, so after warmup the
    /// repeated names ("Name0".."Name99") are already interned.
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class ReaderPoolingBenchmarks
    {
        private readonly FixedWidthReader<SampleModel> _reader = new();
        private readonly FixedWidthReader<SampleModel> _pooledReader = new(new FixedWidthReaderOptions { StringPool = new StringPool() });
        private readonly FixedWidthReader<NumericModel> _numericReader = new();
        private byte[] _bytes = [];
        private byte[] _numericBytes = [];

        [Params(1000)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _bytes = SampleData.Bytes(Count);
            var numeric = SampleData.Models(Count).Select(m => new NumericModel { Age = m.Age, Salary = m.Salary }).ToArray();
            using var ms = new MemoryStream();
            new FixedWidthWriter<NumericModel>().WriteMany(ms, numeric.AsSpan());
            _numericBytes = ms.ToArray();
        }

        /// <summary>String model, no pool: a fresh string decoded per line. Baseline.</summary>
        [Benchmark(Baseline = true)]
        public int WithString_NoPool()
        {
            int sum = 0;
            foreach (var model in _reader.Read(new MemoryStream(_bytes, writable: false)))
            {
                sum += model.Age;
            }
            return sum;
        }

        /// <summary>String model, StringPool: repeated values interned, not re-decoded.</summary>
        [Benchmark]
        public int WithString_Pooled()
        {
            int sum = 0;
            foreach (var model in _pooledReader.Read(new MemoryStream(_bytes, writable: false)))
            {
                sum += model.Age;
            }
            return sum;
        }

        /// <summary>Numeric-only model: no string column, nothing to decode or pool.</summary>
        [Benchmark]
        public int NoString()
        {
            int sum = 0;
            foreach (var model in _numericReader.Read(new MemoryStream(_numericBytes, writable: false)))
            {
                sum += model.Age;
            }
            return sum;
        }
    }
}
