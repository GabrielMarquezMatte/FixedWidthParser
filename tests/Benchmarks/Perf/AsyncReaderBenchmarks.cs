using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser;
using FixedWidthParser.Parsers;
using FixedWidthParser.Readers;
using FixedWidthParser.Writers;

namespace Benchmarks.Perf
{
    /// <summary>
    /// Asynchronous reading: compares the span-based await foreach (FixedWidthReader.ReadAsync)
    /// with the naive baseline ReadLineAsync()+TryParse. In-memory source (StringReader, whose
    /// ReadAsync completes synchronously) to isolate the async state-machine overhead + buffering.
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class AsyncReaderBenchmarks
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private readonly FixedWidthParser<SampleModel> _parser = new();
        private readonly FixedWidthReader<SampleModel> _reader = new(CultureInfo.InvariantCulture);
        private readonly FixedWidthReader<SampleModel> _pooledReader = new(CultureInfo.InvariantCulture, new StringPool());
        private readonly StringPool _generatedStringPool = new();
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

        /// <summary>ReadLineAsync() allocates a string per line; then TryParse. Baseline.</summary>
        [Benchmark(Baseline = true)]
        public async Task<int> Naive_ReadLineAsync()
        {
            using var reader = new StringReader(_text);
            int sum = 0;
            string? line;
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                if (_parser.TryParse(line, Culture, null, out var model)) sum += model.Age;
            }
            return sum;
        }

        /// <summary>Span-based await foreach: slices each line from the buffer, no string per line.</summary>
        [Benchmark]
        public async Task<int> SpanReader_ReadAsync()
        {
            using var reader = new StringReader(_text);
            int sum = 0;
            await foreach (var model in _reader.ReadAsync(reader)) sum += model.Age;
            return sum;
        }

        /// <summary>Span-based await foreach + StringPool: also interns the string columns.</summary>
        [Benchmark]
        public async Task<int> SpanReader_ReadAsync_Pooled()
        {
            using var reader = new StringReader(_text);
            int sum = 0;
            await foreach (var model in _pooledReader.ReadAsync(reader)) sum += model.Age;
            return sum;
        }

        /// <summary>Source-generated async reader: same scanner, static generated TryParse, no reflection.</summary>
        [Benchmark]
        public async Task<int> GeneratedReader_ReadAsync()
        {
            using var reader = new StringReader(_text);
            int sum = 0;
            await foreach (var model in FixedWidth.ReadAsync<GenSampleModel>(reader, formatProvider: Culture)) sum += model.Age;
            return sum;
        }

        /// <summary>Source-generated async reader + StringPool: static TryParse plus interned string columns.</summary>
        [Benchmark]
        public async Task<int> GeneratedReader_ReadAsync_Pooled()
        {
            using var reader = new StringReader(_text);
            int sum = 0;
            await foreach (var model in FixedWidth.ReadAsync<GenSampleModel>(reader, formatProvider: Culture, stringPool: _generatedStringPool)) sum += model.Age;
            return sum;
        }
    }
}
