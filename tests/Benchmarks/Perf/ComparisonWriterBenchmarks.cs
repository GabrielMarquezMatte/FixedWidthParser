using System.Globalization;
using BenchmarkDotNet.Attributes;
using FileHelpers;
using FlatFiles;
using FlatFiles.TypeMapping;
using FixedWidthParser.Writers;
using RecordParser.Parsers;
using RecordParser.Builders.Writer;
using RecordParser.Extensions;

namespace Benchmarks.Perf
{
    public sealed class FlatFilesWriteModel
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public double Salary { get; set; }
    }

    /// <summary>
    /// Cross-library comparison of fixed-width writing: FileHelpers and FlatFiles against
    /// FixedWidthParser. All writes target Stream.Null to isolate formatting from I/O.
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class ComparisonWriterBenchmarks : IDisposable
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private static readonly FixedLengthOptions FlatFilesOptions = new();
        private readonly FixedWidthWriter<SampleModel> _writer = new();
        private readonly FileHelperEngine<FileHelpersRecord> _engine = new();
        private readonly IFixedLengthWriter<SampleModel> _fixedWidthWriter = CreateWriter();
        private StreamWriter _sink = null!;
        private ITypedWriter<FlatFilesWriteModel> _flatFilesWriter = null!;
        private SampleModel[] _models = [];
        private FileHelpersRecord[] _fileHelpersModels = [];
        private FlatFilesWriteModel[] _flatFilesModels = [];
        private bool _disposed;

        private static IFixedLengthWriter<SampleModel> CreateWriter()
        {
            FixedLengthWriterBuilder<SampleModel> builder = new();
            return builder.Map(m => m.Name, 0, 10)
                          .Map(m => m.Age, 10, 5)
                          .Map(m => m.Salary, 15, 10)
                          .Build();
        }

        [Params(1, 100, 1000)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _sink?.Dispose();
            _sink = new StreamWriter(Stream.Null);

            var mapper = FixedLengthTypeMapper.Define<FlatFilesWriteModel>();
            mapper.Property(m => m.Name, new Window(10));
            mapper.Property(m => m.Age, new Window(5));
            mapper.Property(m => m.Salary, new Window(10));
            _flatFilesWriter = mapper.GetWriter(_sink, FlatFilesOptions);

            _models = new SampleModel[Count];
            _fileHelpersModels = new FileHelpersRecord[Count];
            _flatFilesModels = new FlatFilesWriteModel[Count];
            for (int i = 0; i < Count; i++)
            {
                _models[i] = new SampleModel { Name = "Name" + (i % 100), Age = 20 + (i % 50), Salary = 1000.0 + i };
                _fileHelpersModels[i] = new FileHelpersRecord { Name = "Name" + (i % 100), Age = 20 + (i % 50), Salary = 1000.0 + i };
                _flatFilesModels[i] = new FlatFilesWriteModel { Name = "Name" + (i % 100), Age = 20 + (i % 50), Salary = 1000.0 + i };
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _sink?.Dispose();
        }

        [Benchmark(Baseline = true)]
        public void FileHelpers_Write()
        {
            _engine.WriteStream(_sink, _fileHelpersModels);
        }

        [Benchmark]
        public void FlatFiles_Write()
        {
            foreach (var model in _flatFilesModels)
            {
                _flatFilesWriter.Write(model);
            }
        }

        [Benchmark]
        public void RecordParser_Write()
        {
            _sink.WriteRecords(_models, _fixedWidthWriter.TryFormat);
        }

        [Benchmark]
        public void FixedWidthParser_Write()
        {
            _writer.WriteMany(_sink, _models.AsSpan(), Culture);
        }

        public virtual void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _sink?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
