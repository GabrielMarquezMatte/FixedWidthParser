using System.Globalization;
using BenchmarkDotNet.Attributes;
using Benchmarks.Writers;

namespace Benchmarks.Perf
{
    /// <summary>
    /// Mede o caminho quente de escrita fixed-width. Escreve em Stream.Null para isolar
    /// formatação + encoding do custo de I/O em disco. WriteMany_ReuseWriter é o baseline.
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class WriterBenchmarks
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private readonly FixedWidthWriter<SampleModel> _writer = new();
        private StreamWriter _sink = null!;
        private SampleModel[] _models = [];

        [Params(1, 100, 1000)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _sink = new StreamWriter(Stream.Null);
            _models = new SampleModel[Count];
            for (int i = 0; i < Count; i++)
            {
                _models[i] = new SampleModel
                {
                    Name = "Name" + i,
                    Age = 20 + (i % 50),
                    Salary = 1000.0 + i
                };
            }
        }

        [GlobalCleanup]
        public void Cleanup() => _sink.Dispose();

        /// <summary>Cria/descarta um StreamWriter por chamada (sobre a Stream). Baseline.</summary>
        [Benchmark(Baseline = true)]
        public void WriteMany_NewStream()
        {
            _writer.WriteMany(Stream.Null, _models, Culture);
        }

        /// <summary>Reaproveita um StreamWriter já existente (overload novo). Deve eliminar a alocação fixa.</summary>
        [Benchmark]
        public void WriteMany_ReuseWriter()
        {
            _writer.WriteMany(_sink, _models, Culture);
        }

        /// <summary>Reuso de writer + ReadOnlySpan: sem StreamWriter por chamada e sem enumerador. Deve ser zero-alloc.</summary>
        [Benchmark]
        public void WriteMany_ReuseWriterSpan()
        {
            _writer.WriteMany(_sink, _models.AsSpan(), Culture);
        }

        /// <summary>Caminho assíncrono (ArrayPool + WriteLineAsync). Mede o overhead async.</summary>
        [Benchmark]
        public async Task WriteMany_Async()
        {
            await _writer.WriteManyAsync(Stream.Null, _models, Culture);
        }
    }
}
