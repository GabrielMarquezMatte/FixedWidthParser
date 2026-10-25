using BenchmarkDotNet.Attributes;
using FixedWidthParser;

namespace Benchmarks.Perf
{
    /// <summary>
    /// Measures fixed overhead for constructing the read path and consuming a single record.
    /// This is a cold-start proxy: large-file throughput benchmarks dilute the reflection parser
    /// setup cost, while small reads expose it.
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class ReaderStartupBenchmarks
    {
        private static readonly byte[] Text = "John Doe  30   60000.00  \n"u8.ToArray();

        [Benchmark(Baseline = true)]
        public int ReflectionReader_ConstructAndReadOne()
        {
            int sum = 0;
            foreach (var model in FixedWidth.Read(new MemoryStream(Text, writable: false), FixedWidthLayout.FromAttributes<SampleModel>()))
            {
                sum += model.Age;
            }
            return sum;
        }

        [Benchmark]
        public int GeneratedReader_ReadOne()
        {
            int sum = 0;
            foreach (var model in FixedWidth.Read<GenSampleModel>(new MemoryStream(Text, writable: false)))
            {
                sum += model.Age;
            }
            return sum;
        }
    }
}
