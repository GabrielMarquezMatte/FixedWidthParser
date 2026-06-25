using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using FileHelpers;
using FlatFiles;
using FixedWidthParser;
using FixedWidthParser.Readers;
using FixedWidthParser.Writers;
using RecordParser.Parsers;
using RecordParser.Builders.Reader;
using RecordParser.Extensions;

namespace Benchmarks.Perf
{
#pragma warning disable CA1051
    [FixedLengthRecord]
    public sealed class FileHelpersRecord
    {
        [FieldFixedLength(10)]
        [FieldTrim(TrimMode.Both)]
        public string? Name;
        [FieldFixedLength(5)]
        [FieldTrim(TrimMode.Both)]
        public int Age;
        [FieldFixedLength(10)]
        [FieldTrim(TrimMode.Both)]
        public double Salary;
    }
#pragma warning restore CA1051

    /// <summary>
    /// Cross-library comparison: FileHelpers and FlatFiles against FixedWidthParser on the same
    /// in-memory fixed-width data (25 chars/line: 10 string + 5 int + 10 double).
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class ComparisonBenchmarks
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private readonly FixedWidthReader<SampleModel> _reader = new(CultureInfo.InvariantCulture);
        private readonly FileHelperEngine<FileHelpersRecord> _engine = new();
        private readonly FixedLengthSchema _schema = CreateSchema();
        private readonly FixedLengthReaderOptions<SampleModel> _options = CreateReader();
        private string _text = string.Empty;

        [Params(100, 1000)]
        public int Count { get; set; }

        private static FixedLengthSchema CreateSchema()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn(new StringColumn("Name"), new Window(10));
            schema.AddColumn(new Int32Column("Age"), new Window(5));
            schema.AddColumn(new DoubleColumn("Salary"), new Window(10));
            return schema;
        }

        private static FixedLengthReaderOptions<SampleModel> CreateReader()
        {
            FixedLengthReaderBuilder<SampleModel> builder = new();
            var parser = builder.Map(m => m.Name, 0, 10)
                                .Map(m => m.Age, 10, 5)
                                .Map(m => m.Salary, 15, 10)
                                .Build();
            return new (){ Parser = parser.Parse, ParallelismOptions = new() { Enabled = false } };
        }

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

        [Benchmark(Baseline = true)]
        public int FileHelpers_Read()
        {
            var records = _engine.ReadString(_text);
            int sum = 0;
            foreach (var r in records)
            {
                sum += r.Age;
            }

            return sum;
        }

        [Benchmark]
        public int FlatFiles_Read()
        {
            using var stringReader = new StringReader(_text);
            var reader = new FixedLengthReader(stringReader, _schema);
            int sum = 0;
            while (reader.Read())
            {
                var values = reader.GetValues();
                sum += (int)values[1]!;
            }
            return sum;
        }

        [Benchmark]
        public int RecordParser_Read()
        {
            using var stringReader = new StringReader(_text);
            var sum = 0;
            foreach (var record in stringReader.ReadRecords(_options))
            {
                sum += record.Age;
            }
            return sum;
        }

        [Benchmark]
        public int FixedWidthParser_Read()
        {
            using var stringReader = new StringReader(_text);
            int sum = 0;
            foreach (var model in _reader.Read(stringReader))
            {
                sum += model.Age;
            }

            return sum;
        }

        [Benchmark]
        public int FixedWidthParser_Generated()
        {
            using var stringReader = new StringReader(_text);
            int sum = 0;
            foreach (var model in FixedWidth.Read<GenSampleModel>(stringReader, formatProvider: Culture))
            {
                sum += model.Age;
            }

            return sum;
        }
    }
}
