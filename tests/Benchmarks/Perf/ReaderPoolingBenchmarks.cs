using BenchmarkDotNet.Attributes;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser;

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
        private static readonly FixedWidthLayout<SampleModel> _layout = FixedWidthLayout.FromAttributes<SampleModel>();
        private readonly FixedWidthReaderOptions _pooledReaderOptions = new FixedWidthReaderOptions { StringPool = new StringPool() };
        private static readonly FixedWidthLayout<NumericModel> _numericLayout = FixedWidthLayout.FromAttributes<NumericModel>();
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
            FixedWidth.WriteMany(ms, numeric.AsSpan(), FixedWidthLayout.FromAttributes<NumericModel>());
            _numericBytes = ms.ToArray();
        }

        /// <summary>String model, no pool: a fresh string decoded per line. Baseline.</summary>
        [Benchmark(Baseline = true)]
        public int WithString_NoPool()
        {
            int sum = 0;
            foreach (var model in FixedWidth.Read(new MemoryStream(_bytes, writable: false), _layout))
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
            foreach (var model in FixedWidth.Read(new MemoryStream(_bytes, writable: false), _layout, options: _pooledReaderOptions))
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
            foreach (var model in FixedWidth.Read(new MemoryStream(_numericBytes, writable: false), _numericLayout))
            {
                sum += model.Age;
            }
            return sum;
        }
    }
}
