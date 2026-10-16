using System.Collections.Concurrent;
using System.Text;
using FixedWidthParser.Readers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>Parallel aggregation must equal one sequential pass, whatever the partition size.</summary>
    public sealed class ParallelAggregateTests : IDisposable
    {
        private readonly string _path = Path.GetTempFileName();

        public void Dispose()
        {
            File.Delete(_path);
        }

        public sealed class Ages : IRecordAccumulator<Ages, NameAgeModel>
        {
            public List<int> Values { get; } = [];

            public void Add(in NameAgeModel model)
            {
                Values.Add(model.Age);
            }

            public void Merge(Ages following)
            {
                Values.AddRange(following.Values);
            }
        }

        public sealed class GenAges : IRecordAccumulator<GenAges, GenNameAgeModel>
        {
            public List<int> Values { get; } = [];

            public void Add(in GenNameAgeModel model)
            {
                Values.Add(model.Age);
            }

            public void Merge(GenAges following)
            {
                Values.AddRange(following.Values);
            }
        }

        // A BOM, CRLF and LF breaks, empty lines and no final line break: every boundary a partition can cut.
        private static string Content(int lines, int badLine = 0)
        {
            var text = new StringBuilder("﻿");
            for (int i = 1; i <= lines; i++)
            {
                string age = i == badLine ? "XX " : (i % 1000).ToString("D3", Inv);
                text.Append("Name").Append(i % 10).Append(' ').Append(age);
                if (i < lines)
                {
                    text.Append(i % 3 == 0 ? "\r\n" : "\n");
                }
                if (i % 7 == 0)
                {
                    text.Append('\n');
                }
            }
            return text.ToString();
        }

        private List<int> Sequential()
        {
            return [.. new FixedWidthReader<NameAgeModel>().ReadFile(_path).Select(m => m.Age)];
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(10)]
        [InlineData(64)]
        [InlineData(4096)]
        [InlineData(0)]
        public async Task EveryPartitionSize_EqualsTheSequentialRead(int chunkSize)
        {
            File.WriteAllText(_path, Content(2000));
            var parallel = new FixedWidthParallelOptions { DegreeOfParallelism = 4, ChunkSize = chunkSize };

            var runtime = await new FixedWidthReader<NameAgeModel>().AggregateFileAsync<Ages>(_path, parallel, TestContext.Current.CancellationToken);
            var generated = await FixedWidth.AggregateFileAsync<GenNameAgeModel, GenAges>(_path, parallel: parallel, ct: TestContext.Current.CancellationToken);

            var expected = Sequential();
            Assert.Equal(2000, expected.Count);
            Assert.Equal(expected, runtime.Values);
            Assert.Equal(expected, generated.Values);
        }

        [Fact]
        public async Task EmptyFile_ReturnsAnEmptyAccumulator()
        {
            File.WriteAllBytes(_path, []);

            var result = await new FixedWidthReader<NameAgeModel>().AggregateFileAsync<Ages>(_path, ct: TestContext.Current.CancellationToken);

            Assert.Empty(result.Values);
        }

        [Theory]
        [InlineData(5)]
        [InlineData(4096)]
        public async Task ParseFailure_ReportsTheSequentialLineNumber(int chunkSize)
        {
            File.WriteAllText(_path, Content(2000, badLine: 1537));
            var expected = Assert.Throws<FixedWidthParseException>(() => Sequential());
            var parallel = new FixedWidthParallelOptions { DegreeOfParallelism = 4, ChunkSize = chunkSize };

            var actual = await Assert.ThrowsAsync<FixedWidthParseException>(
                () => new FixedWidthReader<NameAgeModel>().AggregateFileAsync<Ages>(_path, parallel, TestContext.Current.CancellationToken));

            Assert.Equal(expected.LineNumber, actual.LineNumber);
            Assert.Equal(expected.Message, actual.Message);
        }

        [Fact]
        public async Task OnParseError_SkipsEachBadLineOnce_WithItsLineNumber()
        {
            File.WriteAllText(_path, Content(2000, badLine: 1537) + "\nbad line\n" + Content(3, badLine: 2)[1..]);
            var sequentialErrors = new List<long>();
            var sequentialOptions = new FixedWidthReaderOptions { OnParseError = ex => { sequentialErrors.Add(ex.LineNumber); return true; } };
            var expected = new FixedWidthReader<NameAgeModel>(sequentialOptions).ReadFile(_path).Select(m => m.Age).ToList();
            var parallelErrors = new ConcurrentBag<long>();
            var parallelOptions = new FixedWidthReaderOptions { OnParseError = ex => { parallelErrors.Add(ex.LineNumber); return true; } };

            var result = await new FixedWidthReader<NameAgeModel>(parallelOptions).AggregateFileAsync<Ages>(
                _path, new FixedWidthParallelOptions { DegreeOfParallelism = 4, ChunkSize = 9 }, TestContext.Current.CancellationToken);

            Assert.Equal(3, sequentialErrors.Count);
            Assert.Equal(expected, result.Values);
            Assert.Equal(sequentialErrors, parallelErrors.Order());
        }

        [Fact]
        public async Task LineTooLong_ReportsTheSequentialLineNumber()
        {
            File.WriteAllText(_path, Content(300) + "\n" + new string('9', 50) + "\n" + Content(3)[1..]);
            var options = new FixedWidthReaderOptions { MaxLineLength = 20 };
            var expected = Assert.Throws<InvalidDataException>(() => new FixedWidthReader<NameAgeModel>(options).ReadFile(_path).ToList());

            var actual = await Assert.ThrowsAsync<InvalidDataException>(() => new FixedWidthReader<NameAgeModel>(options).AggregateFileAsync<Ages>(
                _path, new FixedWidthParallelOptions { DegreeOfParallelism = 4, ChunkSize = 6 }, TestContext.Current.CancellationToken));

            Assert.Equal(expected.Message, actual.Message);
        }

        [Fact]
        public async Task CancelledToken_Throws()
        {
            File.WriteAllText(_path, Content(10));
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => new FixedWidthReader<NameAgeModel>().AggregateFileAsync<Ages>(_path, ct: cts.Token));
        }

        [Fact]
        public async Task NegativeDegreeOfParallelism_Throws()
        {
            File.WriteAllText(_path, Content(10));

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new FixedWidthReader<NameAgeModel>().AggregateFileAsync<Ages>(
                _path, new FixedWidthParallelOptions { DegreeOfParallelism = -1 }, TestContext.Current.CancellationToken));
        }
    }
}
