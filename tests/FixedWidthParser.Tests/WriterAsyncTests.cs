using System.Text;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary><c>WriteManyAsync</c>: output parity with the synchronous path.</summary>
    public class WriterAsyncTests
    {
        private static PersonModel[] SampleModels()
        {
            return [
            new() { Name = "John Doe", Age = 30, Salary = 60000 },
            new() { Name = "Jane",     Age = 28, Salary = 55000 },
            new() { Name = "Max",      Age = 41, Salary = 72000 },
        ];
        }

        [Fact]
        public async Task WriteManyAsync_MatchesSyncOutput()
        {
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();
            var models = SampleModels();

            string sync = WriteMany(layout, (IEnumerable<PersonModel>)models);

            await using var ms = new MemoryStream();
            await FixedWidth.WriteManyAsync(ms, models, layout, null, TestContext.Current.CancellationToken);

            Assert.Equal(sync, Encoding.UTF8.GetString(ms.ToArray()));
        }

        [Fact]
        public async Task WriteManyAsync_LeavesStreamOpen_AndRoundTripsThroughReader()
        {
            await using var ms = new MemoryStream();
            await FixedWidth.WriteManyAsync(ms, SampleModels(), FixedWidthLayout.FromAttributes<PersonModel>(), null, TestContext.Current.CancellationToken);

            Assert.True(ms.CanRead);
            ms.Position = 0;
            var parsed = FixedWidth.Read(ms, FixedWidthLayout.FromAttributes<PersonModel>(), leaveOpen: true).ToList();

            Assert.Equal(3, parsed.Count);
            Assert.Equal("John Doe", parsed[0].Name);
            Assert.Equal(28, parsed[1].Age);
            Assert.Equal(72000.00, parsed[2].Salary, 2);
        }

        [Fact]
        public async Task WriteManyAsync_ManyRecords_SpansSeveralBatches()
        {
            // 5,000 records of 27 bytes exceed the 64 KB batch, so the buffer is flushed mid-way.
            var models = Enumerable.Range(0, 5000).Select(i => new PersonModel { Name = "P" + i, Age = i % 100, Salary = i }).ToList();
            var writerOptions = new FixedWidthWriterOptions { NewLine = "\r\n" };
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();

            await using var ms = new MemoryStream();
            await FixedWidth.WriteManyAsync(ms, models, layout, writerOptions, TestContext.Current.CancellationToken);

            Assert.Equal(5000 * 27, ms.Length);
            Assert.Equal(WriteMany(layout, models, writerOptions), Encoding.UTF8.GetString(ms.ToArray()));
        }

        [Fact]
        public async Task WriteManyAsync_EmptyCollection_WritesNothing()
        {
            await using var ms = new MemoryStream();
            await FixedWidth.WriteManyAsync(ms, [], FixedWidthLayout.FromAttributes<PersonModel>(), null, TestContext.Current.CancellationToken);

            Assert.Equal(0, ms.Length);
        }
    }
}
