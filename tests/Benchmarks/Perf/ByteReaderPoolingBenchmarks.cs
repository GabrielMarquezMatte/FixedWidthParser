using System.Globalization;
using BenchmarkDotNet.Attributes;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Readers;
using FixedWidthParser.Writers;

namespace Benchmarks.Perf
{
    /// <summary>
    /// Where the byte reader's per-line allocation actually comes from — the string column — and the
    /// two ways to remove it: a <see cref="StringPool"/> (intern repeated values) or a model with no
    /// string at all. End-to-end stream read over an in-memory <see cref="MemoryStream"/>. The pool is
    /// reused across invocations, so after warmup the repeated names ("Name0".."Name99") are already
    /// interned and the pooled lane allocates ~nothing per line.
    /// <list type="bullet">
    /// <item><c>WithString_NoPool</c>: <see cref="SampleModel"/>, a fresh string decoded per line (baseline);</item>
    /// <item><c>WithString_Pooled</c>: same model + StringPool — the string is interned, not re-decoded;</item>
    /// <item><c>NoString</c>: <see cref="NumericModel"/> — no string column, nothing to decode or pool.</item>
    /// </list>
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class ByteReaderPoolingBenchmarks
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private readonly FixedWidthByteReader<SampleModel> _reader = new(CultureInfo.InvariantCulture);
        private readonly FixedWidthByteReader<SampleModel> _pooledReader = new(CultureInfo.InvariantCulture, new StringPool());
        private readonly FixedWidthByteReader<NumericModel> _numericReader = new(CultureInfo.InvariantCulture);
        private MemoryStream _stream = new();
        private MemoryStream _numericStream = new();

        [Params(100, 1000)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            var models = new SampleModel[Count];
            var numeric = new NumericModel[Count];
            for (int i = 0; i < Count; i++)
            {
                models[i] = new SampleModel { Name = "Name" + (i % 100), Age = 20 + (i % 50), Salary = 1000.0 + i };
                numeric[i] = new NumericModel { Age = 20 + (i % 50), Salary = 1000.0 + i };
            }

            _stream = new MemoryStream();
            new FixedWidthWriter<SampleModel>().WriteMany(_stream, models.AsSpan(), Culture);

            _numericStream = new MemoryStream();
            new FixedWidthWriter<NumericModel>().WriteMany(_numericStream, numeric.AsSpan(), Culture);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _stream.Dispose();
            _numericStream.Dispose();
        }

        /// <summary>Byte reader, string model, no pool: a fresh string decoded per line. Baseline.</summary>
        [Benchmark(Baseline = true)]
        public int WithString_NoPool()
        {
            _stream.Position = 0;
            int sum = 0;
            foreach (var model in _reader.Read(_stream, leaveOpen: true)) sum += model.Age;
            return sum;
        }

        /// <summary>Byte reader, string model, StringPool: repeated values interned, not re-decoded.</summary>
        [Benchmark]
        public int WithString_Pooled()
        {
            _stream.Position = 0;
            int sum = 0;
            foreach (var model in _pooledReader.Read(_stream, leaveOpen: true)) sum += model.Age;
            return sum;
        }

        /// <summary>Byte reader, numeric-only model: no string column, nothing to decode or pool.</summary>
        [Benchmark]
        public int NoString()
        {
            _numericStream.Position = 0;
            int sum = 0;
            foreach (var model in _numericReader.Read(_numericStream, leaveOpen: true)) sum += model.Age;
            return sum;
        }
    }
}
