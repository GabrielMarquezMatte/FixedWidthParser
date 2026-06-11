using System.Text;
using FixedWidthParser.Readers;
using FixedWidthParser.Writers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Covers the asynchronous batch write path (<c>WriteManyAsync</c>) for both the Stream and
    /// StreamWriter overloads, checking output parity with the synchronous path.
    /// </summary>
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
        public async Task WriteManyAsync_Stream_MatchesSyncOutput()
        {
            var writer = new FixedWidthWriter<PersonModel>();
            var models = SampleModels();

            string sync = WriteMany(writer, (IEnumerable<PersonModel>)models);

            await using var ms = new MemoryStream();
            await writer.WriteManyAsync(ms, models, Inv).ConfigureAwait(true);
            string asyncResult = Encoding.UTF8.GetString(ms.ToArray());

            Assert.Equal(sync, asyncResult);
        }

        [Fact]
        public async Task WriteManyAsync_StreamWriter_MatchesSyncOutput()
        {
            var writer = new FixedWidthWriter<PersonModel>();
            var models = SampleModels();

            string sync = WriteMany(writer, (IEnumerable<PersonModel>)models);

            await using var ms = new MemoryStream();
            await using (var sw = new StreamWriter(ms, leaveOpen: true))
            {
                await writer.WriteManyAsync(sw, models, Inv).ConfigureAwait(true);
            }
            string asyncResult = Encoding.UTF8.GetString(ms.ToArray());

            Assert.Equal(sync, asyncResult);
        }

        [Fact]
        public async Task WriteManyAsync_Stream_LeavesStreamOpen_AndRoundTripsThroughReader()
        {
            var writer = new FixedWidthWriter<PersonModel>();
            var reader = new FixedWidthReader<PersonModel>(Inv);
            var models = SampleModels();

            await using var ms = new MemoryStream();
            await writer.WriteManyAsync(ms, models, Inv).ConfigureAwait(true);

            // The Stream overload uses leaveOpen: true, so the stream stays usable.
            Assert.True(ms.CanRead);
            ms.Position = 0;
            var parsed = reader.Read(ms, leaveOpen: true).ToList();

            Assert.Equal(3, parsed.Count);
            Assert.Equal("John Doe", parsed[0].Name);
            Assert.Equal(28, parsed[1].Age);
            Assert.Equal(72000.00, parsed[2].Salary, 2);
        }

        [Fact]
        public async Task WriteManyAsync_Stream_EmptyCollection_WritesNothing()
        {
            var writer = new FixedWidthWriter<PersonModel>();

            await using var ms = new MemoryStream();
            await writer.WriteManyAsync(ms, [], Inv).ConfigureAwait(true);

            Assert.Equal(0, ms.Length);
        }

        [Fact]
        public async Task WriteManyAsync_StreamWriter_EmptyCollection_WritesNothing()
        {
            var writer = new FixedWidthWriter<PersonModel>();

            await using var ms = new MemoryStream();
            await using (var sw = new StreamWriter(ms, leaveOpen: true))
            {
                await writer.WriteManyAsync(sw, [], Inv).ConfigureAwait(true);
            }

            Assert.Equal(0, ms.Length);
        }
    }
}
