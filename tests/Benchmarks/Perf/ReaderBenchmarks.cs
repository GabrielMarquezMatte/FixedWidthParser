using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Parsers;
using FixedWidthParser.Readers;
using FixedWidthParser.Writers;

namespace Benchmarks.Perf
{
    /// <summary>
    /// Synchronous reading: compares the span-based path (FixedWidthReader, no string per line)
    /// with the naive baseline ReadLine()+TryParse (one string allocated per line). The source is
    /// an in-memory string (StringReader) to isolate parsing+buffering from the cost of disk I/O.
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class ReaderBenchmarks
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private readonly FixedWidthParser<SampleModel> _parser = new();
        private readonly FixedWidthReader<SampleModel> _reader = new(CultureInfo.InvariantCulture);
        private readonly FixedWidthReader<SampleModel> _pooledReader = new(CultureInfo.InvariantCulture, new StringPool());
        private string _text = string.Empty;

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
            using var ms = new MemoryStream();
            writer.WriteMany(ms, models.AsSpan(), Culture);
            _text = Encoding.UTF8.GetString(ms.ToArray());
        }

        /// <summary>ReadLine() allocates a string per line; then TryParse over it. Baseline.</summary>
        [Benchmark(Baseline = true)]
        public int Naive_ReadLine()
        {
            using var reader = new StringReader(_text);
            int sum = 0;
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (_parser.TryParse(line, Culture, null, out var model)) sum += model.Age;
            }
            return sum;
        }

        /// <summary>Span-based reading: slices each line from the buffer, no string per line.</summary>
        [Benchmark]
        public int SpanReader_Read()
        {
            using var reader = new StringReader(_text);
            int sum = 0;
            foreach (var model in _reader.Read(reader)) sum += model.Age;
            return sum;
        }

        /// <summary>Span-based + StringPool: also interns the string columns (tends to zero-alloc).</summary>
        [Benchmark]
        public int SpanReader_Read_Pooled()
        {
            using var reader = new StringReader(_text);
            int sum = 0;
            foreach (var model in _pooledReader.Read(reader)) sum += model.Age;
            return sum;
        }
    }
}
