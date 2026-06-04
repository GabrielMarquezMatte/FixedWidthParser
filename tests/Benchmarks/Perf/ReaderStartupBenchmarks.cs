using System.Globalization;
using BenchmarkDotNet.Attributes;
using FixedWidthParser;
using FixedWidthParser.Readers;

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
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private const string Text = "John Doe  30   60000.00  \n";

        [Benchmark(Baseline = true)]
        public int ReflectionReader_ConstructAndReadOne()
        {
            var fixedWidthReader = new FixedWidthReader<SampleModel>(Culture);
            using var reader = new StringReader(Text);
            int sum = 0;
            foreach (var model in fixedWidthReader.Read(reader)) sum += model.Age;
            return sum;
        }

        [Benchmark]
        public int GeneratedReader_ReadOne()
        {
            using var reader = new StringReader(Text);
            int sum = 0;
            foreach (var model in FixedWidth.Read<GenSampleModel>(reader, formatProvider: Culture)) sum += model.Age;
            return sum;
        }
    }
}
