using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using FixedWidthParser.Parsers;
using FixedWidthParser.Writers;

namespace Benchmarks.Perf
{
    /// <summary>
    /// UTF-8 / byte parsing vs the char path. Isolates the per-line parse from I/O: lines are
    /// pre-split in <see cref="Setup"/> into char[] and UTF-8 byte[] forms (ASCII fixtures, so byte
    /// offsets equal char offsets). Three lanes:
    /// <list type="bullet">
    /// <item>baseline: parse lines that are already <c>char</c> (no transcode at all);</item>
    /// <item>char-after-decode: UTF-8 → string per line, then parse — the cost a StreamReader-backed
    /// char reader effectively pays on a UTF-8 source;</item>
    /// <item>byte: parse straight from UTF-8 bytes with <see cref="Utf8FixedWidthParser{TModel}"/> —
    /// the transcode is skipped entirely.</item>
    /// </list>
    /// This measures the parse core only; <see cref="ByteReaderStreamBenchmarks"/> covers the
    /// end-to-end stream read (StreamReader-backed char reader vs the raw-byte reader).
    /// </summary>
    [Config(typeof(RegressionConfig))]
    public class ByteReaderBenchmarks
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private readonly FixedWidthParser<SampleModel> _charParser = new();
        private readonly Utf8FixedWidthParser<SampleModel> _byteParser = new();

        private string[] _charLines = [];
        private byte[][] _byteLines = [];

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

            var text = Encoding.UTF8.GetString(ms.ToArray());
            _charLines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < _charLines.Length; i++)
            {
                _charLines[i] = _charLines[i].TrimEnd('\r');
            }
            _byteLines = new byte[_charLines.Length][];
            for (int i = 0; i < _charLines.Length; i++)
            {
                _byteLines[i] = Encoding.UTF8.GetBytes(_charLines[i]);
            }
        }

        /// <summary>Char parse over lines that are already UTF-16: no transcode. Baseline.</summary>
        [Benchmark(Baseline = true)]
        public int CharParser_Parse()
        {
            int sum = 0;
            foreach (var line in _charLines)
            {
                if (_charParser.TryParse(line, Culture, null, out var model)) sum += model.Age;
            }
            return sum;
        }

        /// <summary>UTF-8 → string per line, then char parse: the transcode a char reader pays on a UTF-8 source.</summary>
        [Benchmark]
        public int CharParser_Parse_AfterUtf8Decode()
        {
            int sum = 0;
            foreach (var bytes in _byteLines)
            {
                var line = Encoding.UTF8.GetString(bytes);
                if (_charParser.TryParse(line, Culture, null, out var model)) sum += model.Age;
            }
            return sum;
        }

        /// <summary>Byte parse straight from UTF-8: no transcode, no string per line.</summary>
        [Benchmark]
        public int ByteParser_Parse()
        {
            int sum = 0;
            foreach (var bytes in _byteLines)
            {
                if (_byteParser.TryParse(bytes, Culture, out var model)) sum += model.Age;
            }
            return sum;
        }
    }
}
