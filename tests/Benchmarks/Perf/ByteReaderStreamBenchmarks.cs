using System.Globalization;
using BenchmarkDotNet.Attributes;
using FixedWidthParser.Readers;
using FixedWidthParser.Writers;

namespace Benchmarks.Perf
{
    /// <summary>
    /// End-to-end streaming read: the char <see cref="FixedWidthReader{TModel}"/> (which decodes the
    /// stream UTF-8 → UTF-16 through a <see cref="StreamReader"/>) versus the raw-byte
    /// <see cref="FixedWidthByteReader{TModel}"/> (which slices lines straight from the byte buffer,
    /// no transcode). The source is an in-memory <see cref="MemoryStream"/> so the comparison isolates
    /// buffering+decoding+parsing from disk I/O. Both readers are rewound per invocation.
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class ByteReaderStreamBenchmarks
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private readonly FixedWidthReader<SampleModel> _charReader = new(CultureInfo.InvariantCulture);
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

        /// <summary>Char reader: StreamReader decodes UTF-8 → UTF-16, then span slicing. Baseline.</summary>
        [Benchmark(Baseline = true)]
        public int CharReader_Read()
        {
            _stream.Position = 0;
            int sum = 0;
            foreach (var model in _charReader.Read(_stream, leaveOpen: true))
            {
                sum += model.Age;
            }

            return sum;
        }

        /// <summary>Byte reader: slices lines straight from the byte buffer, no transcode, no string per line.</summary>
        [Benchmark]
        public int ByteReader_Read()
        {
            _stream.Position = 0;
            int sum = 0;
            foreach (var model in _byteReader.Read(_stream, leaveOpen: true))
            {
                sum += model.Age;
            }

            return sum;
        }
    }
}
