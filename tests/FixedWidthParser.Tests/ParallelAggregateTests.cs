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

        /// <summary>Stalls on the first line and counts the accumulators not yet merged into another.</summary>
        public sealed class SlowFirstChunk : IRecordAccumulator<SlowFirstChunk, NameAgeModel>
        {
            public static int Unmerged;
            public static int MaxUnmerged;

            public SlowFirstChunk()
            {
                int unmerged = Interlocked.Increment(ref Unmerged);
                int max;
                while (unmerged > (max = Volatile.Read(ref MaxUnmerged)) && Interlocked.CompareExchange(ref MaxUnmerged, unmerged, max) != max)
                {
                }
            }

            public List<int> Values { get; } = [];

            public void Add(in NameAgeModel model)
            {
                if (model.Name == "Name1" && model.Age == 1)
                {
                    Thread.Sleep(300);
                }
                Values.Add(model.Age);
            }

            public void Merge(SlowFirstChunk following)
            {
                Values.AddRange(following.Values);
                Interlocked.Decrement(ref Unmerged);
            }
        }

        public sealed class CancelsItself : IRecordAccumulator<CancelsItself, NameAgeModel>
        {
            public void Add(in NameAgeModel model)
            {
                if (model.Age == 7)
                {
                    throw new OperationCanceledException();
                }
            }

            public void Merge(CancelsItself following)
            {
            }
        }

        public sealed class ThrowsOnAdd : IRecordAccumulator<ThrowsOnAdd, NameAgeModel>
        {
            public void Add(in NameAgeModel model)
            {
                throw new InvalidOperationException("Add failed");
            }

            public void Merge(ThrowsOnAdd following)
            {
            }
        }

        public sealed class ThrowsOnMerge : IRecordAccumulator<ThrowsOnMerge, NameAgeModel>
        {
            public void Add(in NameAgeModel model)
            {
            }

            public void Merge(ThrowsOnMerge following)
            {
                throw new InvalidOperationException("Merge failed");
            }
        }

        /// <summary>A live, non-seekable source: hands over its prefix, then waits for data until cancelled.</summary>
        private sealed class OpenEndedStream(byte[] prefix) : Stream
        {
            private int _position;

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (_position < prefix.Length)
                {
                    int count = Math.Min(buffer.Length, prefix.Length - _position);
                    prefix.AsMemory(_position, count).CopyTo(buffer);
                    _position += count;
                    return count;
                }
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                return 0;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }

        public enum Failure
        {
            Parse,
            Add,
            Merge,
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

        private Task<Ages> Aggregate(Source source, FixedWidthReaderOptions options)
        {
            var layout = FixedWidthLayout.FromAttributes<NameAgeModel>();
            var ct = TestContext.Current.CancellationToken;
            return source switch
            {
                Source.File => FixedWidth.AggregateFileAsync<NameAgeModel, Ages>(_path, layout, options, ct),
                _ => FixedWidth.AggregateAsync<NameAgeModel, Ages>(File.OpenRead(_path), layout, leaveOpen: false, options, ct),
            };
        }

        private Task<GenAges> AggregateGenerated(Source source, FixedWidthReaderOptions options)
        {
            var ct = TestContext.Current.CancellationToken;
            return source switch
            {
                Source.File => FixedWidth.AggregateFileAsync<GenNameAgeModel, GenAges>(_path, options, ct),
                _ => FixedWidth.AggregateAsync<GenNameAgeModel, GenAges>(File.OpenRead(_path), options: options, cancellationToken: ct),
            };
        }

        [Theory]
        [MemberData(nameof(SourcesAndChunkSizes))]
        public async Task EveryPartitionSize_EqualsTheSequentialRead(Source source, int chunkSize)
        {
            File.WriteAllText(_path, Content(2000));
            var parallel = new FixedWidthReaderOptions { DegreeOfParallelism = 4, ChunkSize = chunkSize };

            var runtime = await Aggregate(source, parallel);
            var generated = await AggregateGenerated(source, parallel);

            var expected = Sequential();
            Assert.Equal(2000, expected.Count);
            Assert.Equal(expected, runtime.Values);
            Assert.Equal(expected, generated.Values);
        }

        // Hundreds of chunks finish while the first one stalls; only a few slots' worth may wait for it.
        [Fact]
        public async Task SlowEarlyChunk_BoundsTheResultsWaitingToMerge()
        {
            File.WriteAllText(_path, Content(2000));
            var options = new FixedWidthReaderOptions { DegreeOfParallelism = 4, ChunkSize = 64 };

            var result = await FixedWidth.AggregateAsync<NameAgeModel, SlowFirstChunk>(
                File.OpenRead(_path), FixedWidthLayout.FromAttributes<NameAgeModel>(), leaveOpen: false, options, TestContext.Current.CancellationToken);

            Assert.Equal(Sequential(), result.Values);
            Assert.InRange(SlowFirstChunk.MaxUnmerged, 1, 4 * 4 + 1);
        }

        // Not the caller's cancellation: it fails its chunk like any exception instead of stalling the producer.
        [Fact]
        public async Task AccumulatorThrowingOperationCanceled_FailsTheAggregation()
        {
            File.WriteAllText(_path, Content(2000));
            var options = new FixedWidthReaderOptions { DegreeOfParallelism = 4, ChunkSize = 64 };

            await Assert.ThrowsAsync<OperationCanceledException>(() => FixedWidth.AggregateAsync<NameAgeModel, CancelsItself>(
                File.OpenRead(_path), FixedWidthLayout.FromAttributes<NameAgeModel>(), leaveOpen: false, options, TestContext.Current.CancellationToken));
        }

        [Theory]
        [MemberData(nameof(Sources))]
        public async Task OneWorker_EqualsTheSequentialRead(Source source)
        {
            File.WriteAllText(_path, Content(500));

            var result = await Aggregate(source, new FixedWidthReaderOptions { DegreeOfParallelism = 1 });

            Assert.Equal(Sequential(), result.Values);
        }

        [Theory]
        [MemberData(nameof(Sources))]
        public async Task EmptySource_ReturnsAnEmptyAccumulator(Source source)
        {
            File.WriteAllBytes(_path, []);

            var result = await Aggregate(source, new FixedWidthReaderOptions { DegreeOfParallelism = 4, ChunkSize = 8 });

            Assert.Empty(result.Values);
        }

        [Theory]
        [MemberData(nameof(SourcesAndChunkSizes))]
        public async Task ParseFailure_ReportsTheSequentialLineNumber(Source source, int chunkSize)
        {
            File.WriteAllText(_path, Content(2000, badLine: 1537));
            var expected = Assert.Throws<FixedWidthParseException>(() => Sequential());
            var parallel = new FixedWidthReaderOptions { DegreeOfParallelism = 4, ChunkSize = chunkSize };

            var actual = await Assert.ThrowsAsync<FixedWidthParseException>(() => Aggregate(source, parallel));

            Assert.Equal(expected.LineNumber, actual.LineNumber);
            Assert.Equal(expected.Message, actual.Message);
        }

        // Chunks of 16 bytes hold one line each: the source hands over three before it waits. A failure that decides
        // the result must end the read there, not when the stream ends, which this one never does.
        [Theory]
        [InlineData(Failure.Parse)]
        [InlineData(Failure.Add)]
        [InlineData(Failure.Merge)]
        public async Task FatalFailure_StopsReadingAnOpenStream(Failure failure)
        {
            byte[] prefix = Encoding.ASCII.GetBytes((failure == Failure.Parse ? "Name1 XX \n" : "Name1 001\n") + "Name2 002\nName3 003\nName4 004\n");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var layout = FixedWidthLayout.FromAttributes<NameAgeModel>();
            var options = new FixedWidthReaderOptions { DegreeOfParallelism = 2, ChunkSize = 16 };
            await using var stream = new OpenEndedStream(prefix);

            var thrown = await Record.ExceptionAsync(() => failure switch
            {
                Failure.Add => FixedWidth.AggregateAsync<NameAgeModel, ThrowsOnAdd>(stream, layout, leaveOpen: false, options, timeout.Token),
                Failure.Merge => FixedWidth.AggregateAsync<NameAgeModel, ThrowsOnMerge>(stream, layout, leaveOpen: false, options, timeout.Token),
                _ => FixedWidth.AggregateAsync<NameAgeModel, Ages>(stream, layout, leaveOpen: false, options, timeout.Token),
            });

            Assert.False(timeout.IsCancellationRequested, "The failure waited for the stream to end.");
            Assert.IsType(failure == Failure.Parse ? typeof(FixedWidthParseException) : typeof(InvalidOperationException), thrown);
        }

        [Theory]
        [MemberData(nameof(Sources))]
        public async Task OnParseError_SkipsEachBadLineOnce_WithItsLineNumber(Source source)
        {
            File.WriteAllText(_path, Content(2000, badLine: 1537) + "\nbad line\n" + Content(3, badLine: 2)[1..]);
            var sequentialErrors = new List<long>();
            var expected = Sequential(new FixedWidthReaderOptions { OnParseError = ex => { sequentialErrors.Add(ex.LineNumber); return true; } });
            var parallelErrors = new ConcurrentBag<long>();
            var options = new FixedWidthReaderOptions
            {
                OnParseError = ex => { parallelErrors.Add(ex.LineNumber); return true; },
                DegreeOfParallelism = 4,
                ChunkSize = 9,
            };

            var result = await Aggregate(source, options);

            Assert.Equal(3, sequentialErrors.Count);
            Assert.Equal(expected, result.Values);
            Assert.Equal(sequentialErrors, parallelErrors.Order());
        }

        [Theory]
        [MemberData(nameof(SourcesAndChunkSizes))]
        public async Task LineTooLong_ReportsTheSequentialLineNumber(Source source, int chunkSize)
        {
            File.WriteAllText(_path, Content(300) + "\n" + new string('9', 50) + "\n" + Content(3)[1..]);
            var options = new FixedWidthReaderOptions { MaxLineLength = 20, DegreeOfParallelism = 4, ChunkSize = chunkSize };
            var expected = Assert.Throws<InvalidDataException>(() => Sequential(options));

            var actual = await Assert.ThrowsAsync<InvalidDataException>(() => Aggregate(source, options));

            Assert.Equal(expected.Message, actual.Message);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Stream_IsDisposedUnlessLeftOpen(bool leaveOpen)
        {
            File.WriteAllText(_path, Content(10));
            await using var stream = File.OpenRead(_path);

            await FixedWidth.AggregateAsync<NameAgeModel, Ages>(stream, FixedWidthLayout.FromAttributes<NameAgeModel>(), leaveOpen, new FixedWidthReaderOptions { DegreeOfParallelism = 2, ChunkSize = 16 }, TestContext.Current.CancellationToken);

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
            var parallel = new FixedWidthReaderOptions { DegreeOfParallelism = 2 };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source switch
            {
                Source.File => FixedWidth.AggregateFileAsync<NameAgeModel, Ages>(_path, layout, parallel, cts.Token),
                _ => FixedWidth.AggregateAsync<NameAgeModel, Ages>(File.OpenRead(_path), layout, leaveOpen: false, parallel, cts.Token),
            });
        }

        // Arguments are validated before the stream is taken over, so the caller still owns it.
        [Fact]
        public async Task NegativeDegreeOfParallelism_Throws()
        {
            File.WriteAllText(_path, Content(10));
            var layout = FixedWidthLayout.FromAttributes<NameAgeModel>();
            var parallel = new FixedWidthReaderOptions { DegreeOfParallelism = -1 };
            var ct = TestContext.Current.CancellationToken;
            await using var stream = File.OpenRead(_path);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => FixedWidth.AggregateFileAsync<NameAgeModel, Ages>(_path, layout, parallel, ct));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => FixedWidth.AggregateAsync<NameAgeModel, Ages>(stream, layout, leaveOpen: false, parallel, ct));
            Assert.True(stream.CanRead);
        }
    }
}
