using System.Collections.Concurrent;
using System.Text;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>Parallel aggregation must equal one sequential pass, whatever the source and partition size.</summary>
    public sealed class ParallelAggregateTests : IDisposable
    {
        public enum Source
        {
            File,
            Stream,
        }

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

        public static TheoryData<Source, int> SourcesAndChunkSizes()
        {
            var data = new TheoryData<Source, int>();
            foreach (var source in Enum.GetValues<Source>())
            {
                foreach (int chunkSize in new[] { 1, 7, 10, 64, 4096, 0 })
                {
                    data.Add(source, chunkSize);
                }
            }
            return data;
        }

        public static TheoryData<Source> Sources()
        {
            return new TheoryData<Source>(Enum.GetValues<Source>());
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

        private List<int> Sequential(FixedWidthReaderOptions? options = null)
        {
            return [.. FixedWidth.ReadFile(_path, FixedWidthLayout.FromAttributes<NameAgeModel>(), options).Select(m => m.Age)];
        }

        private Task<Ages> Aggregate(Source source, FixedWidthParallelOptions parallel, FixedWidthReaderOptions? options = null)
        {
            var layout = FixedWidthLayout.FromAttributes<NameAgeModel>();
            var ct = TestContext.Current.CancellationToken;
            return source switch
            {
                Source.File => FixedWidth.AggregateFileAsync<NameAgeModel, Ages>(_path, layout, options, parallel, ct),
                _ => FixedWidth.AggregateAsync<NameAgeModel, Ages>(File.OpenRead(_path), layout, leaveOpen: false, options, parallel, ct),
            };
        }

        private Task<GenAges> AggregateGenerated(Source source, FixedWidthParallelOptions parallel)
        {
            var ct = TestContext.Current.CancellationToken;
            return source switch
            {
                Source.File => FixedWidth.AggregateFileAsync<GenNameAgeModel, GenAges>(_path, parallel: parallel, cancellationToken: ct),
                _ => FixedWidth.AggregateAsync<GenNameAgeModel, GenAges>(File.OpenRead(_path), parallel: parallel, cancellationToken: ct),
            };
        }

        [Theory]
        [MemberData(nameof(SourcesAndChunkSizes))]
        public async Task EveryPartitionSize_EqualsTheSequentialRead(Source source, int chunkSize)
        {
            File.WriteAllText(_path, Content(2000));
            var parallel = new FixedWidthParallelOptions { DegreeOfParallelism = 4, ChunkSize = chunkSize };

            var runtime = await Aggregate(source, parallel);
            var generated = await AggregateGenerated(source, parallel);

            var expected = Sequential();
            Assert.Equal(2000, expected.Count);
            Assert.Equal(expected, runtime.Values);
            Assert.Equal(expected, generated.Values);
        }

        [Theory]
        [MemberData(nameof(Sources))]
        public async Task OneWorker_EqualsTheSequentialRead(Source source)
        {
            File.WriteAllText(_path, Content(500));

            var result = await Aggregate(source, new FixedWidthParallelOptions { DegreeOfParallelism = 1 });

            Assert.Equal(Sequential(), result.Values);
        }

        [Theory]
        [MemberData(nameof(Sources))]
        public async Task EmptySource_ReturnsAnEmptyAccumulator(Source source)
        {
            File.WriteAllBytes(_path, []);

            var result = await Aggregate(source, new FixedWidthParallelOptions { DegreeOfParallelism = 4, ChunkSize = 8 });

            Assert.Empty(result.Values);
        }

        [Theory]
        [MemberData(nameof(SourcesAndChunkSizes))]
        public async Task ParseFailure_ReportsTheSequentialLineNumber(Source source, int chunkSize)
        {
            File.WriteAllText(_path, Content(2000, badLine: 1537));
            var expected = Assert.Throws<FixedWidthParseException>(() => Sequential());
            var parallel = new FixedWidthParallelOptions { DegreeOfParallelism = 4, ChunkSize = chunkSize };

            var actual = await Assert.ThrowsAsync<FixedWidthParseException>(() => Aggregate(source, parallel));

            Assert.Equal(expected.LineNumber, actual.LineNumber);
            Assert.Equal(expected.Message, actual.Message);
        }

        [Theory]
        [MemberData(nameof(Sources))]
        public async Task OnParseError_SkipsEachBadLineOnce_WithItsLineNumber(Source source)
        {
            File.WriteAllText(_path, Content(2000, badLine: 1537) + "\nbad line\n" + Content(3, badLine: 2)[1..]);
            var sequentialErrors = new List<long>();
            var expected = Sequential(new FixedWidthReaderOptions { OnParseError = ex => { sequentialErrors.Add(ex.LineNumber); return true; } });
            var parallelErrors = new ConcurrentBag<long>();
            var options = new FixedWidthReaderOptions { OnParseError = ex => { parallelErrors.Add(ex.LineNumber); return true; } };

            var result = await Aggregate(source, new FixedWidthParallelOptions { DegreeOfParallelism = 4, ChunkSize = 9 }, options);

            Assert.Equal(3, sequentialErrors.Count);
            Assert.Equal(expected, result.Values);
            Assert.Equal(sequentialErrors, parallelErrors.Order());
        }

        [Theory]
        [MemberData(nameof(SourcesAndChunkSizes))]
        public async Task LineTooLong_ReportsTheSequentialLineNumber(Source source, int chunkSize)
        {
            File.WriteAllText(_path, Content(300) + "\n" + new string('9', 50) + "\n" + Content(3)[1..]);
            var options = new FixedWidthReaderOptions { MaxLineLength = 20 };
            var expected = Assert.Throws<InvalidDataException>(() => Sequential(options));
            var parallel = new FixedWidthParallelOptions { DegreeOfParallelism = 4, ChunkSize = chunkSize };

            var actual = await Assert.ThrowsAsync<InvalidDataException>(() => Aggregate(source, parallel, options));

            Assert.Equal(expected.Message, actual.Message);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Stream_IsDisposedUnlessLeftOpen(bool leaveOpen)
        {
            File.WriteAllText(_path, Content(10));
            await using var stream = File.OpenRead(_path);

            await FixedWidth.AggregateAsync<NameAgeModel, Ages>(stream, FixedWidthLayout.FromAttributes<NameAgeModel>(), leaveOpen, null, new FixedWidthParallelOptions { DegreeOfParallelism = 2, ChunkSize = 16 }, TestContext.Current.CancellationToken);

            Assert.Equal(leaveOpen, stream.CanRead);
        }

        [Theory]
        [MemberData(nameof(Sources))]
        public async Task CancelledToken_Throws(Source source)
        {
            File.WriteAllText(_path, Content(10));
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            var layout = FixedWidthLayout.FromAttributes<NameAgeModel>();
            var parallel = new FixedWidthParallelOptions { DegreeOfParallelism = 2 };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source switch
            {
                Source.File => FixedWidth.AggregateFileAsync<NameAgeModel, Ages>(_path, layout, null, parallel, cts.Token),
                _ => FixedWidth.AggregateAsync<NameAgeModel, Ages>(File.OpenRead(_path), layout, leaveOpen: false, null, parallel, cts.Token),
            });
        }

        // Arguments are validated before the stream is taken over, so the caller still owns it.
        [Fact]
        public async Task NegativeDegreeOfParallelism_Throws()
        {
            File.WriteAllText(_path, Content(10));
            var layout = FixedWidthLayout.FromAttributes<NameAgeModel>();
            var parallel = new FixedWidthParallelOptions { DegreeOfParallelism = -1 };
            var ct = TestContext.Current.CancellationToken;
            await using var stream = File.OpenRead(_path);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => FixedWidth.AggregateFileAsync<NameAgeModel, Ages>(_path, layout, null, parallel, ct));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => FixedWidth.AggregateAsync<NameAgeModel, Ages>(stream, layout, leaveOpen: false, null, parallel, ct));
            Assert.True(stream.CanRead);
        }
    }
}
