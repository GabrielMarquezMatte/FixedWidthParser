using System.IO.Pipelines;
using System.Text;
using FixedWidthParser.Readers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Covers the <see cref="PipeReader"/> overloads of <see cref="FixedWidthByteReader{TModel}"/>
    /// (reflection) and <see cref="FixedWidthParser.FixedWidthUtf8"/> (source-generated): line-ending
    /// handling, BOM skipping, empty-line skipping, multi-segment lines (the pooled scratch-copy path),
    /// cancellation, reader-completion behavior, and parity with the stream reader.
    /// </summary>
    public class PipeReaderTests
    {
        private const string TwoPeople =
            "John Doe  30   60000.00  \n" +
            "Jane      28   55000.00  ";

#pragma warning disable IDISP001 // ownership of the stream transfers to the PipeReader / reader
        private static PipeReader Pipe(string text, int bufferSize = -1)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            return bufferSize > 0
                ? PipeReader.Create(stream, new StreamPipeReaderOptions(bufferSize: bufferSize))
                : PipeReader.Create(stream);
        }

        private static MemoryStream StreamOf(string text)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(text));
        }
#pragma warning restore IDISP001

        private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
        {
            var list = new List<T>();
            await foreach (var item in source.ConfigureAwait(false))
            {
                list.Add(item);
            }
            return list;
        }

        [Fact]
        public async Task ReadAsync_Pipe_ParsesAll()
        {
            var reader = new FixedWidthByteReader<PersonModel>(Inv);

            var people = await CollectAsync(reader.ReadAsync(Pipe(TwoPeople))).ConfigureAwait(true);

            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal(30, people[0].Age);
            Assert.Equal(60000.00, people[0].Salary, 2);
            Assert.Equal("Jane", people[1].Name);
            Assert.Equal(55000.00, people[1].Salary, 2);
        }

        [Fact]
        public async Task ReadAsync_Pipe_Generated_ParsesAll()
        {
            var people = await CollectAsync(
                FixedWidthParser.FixedWidthUtf8.ReadAsync<GenPersonModel>(Pipe(TwoPeople), formatProvider: Inv))
                .ConfigureAwait(true);

            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal(55000.00, people[1].Salary, 2);
        }

        [Theory]
        [InlineData("ABC\nDEF\n")]      // LF + trailing newline
        [InlineData("ABC\r\nDEF\r\n")]  // CRLF
        [InlineData("ABC\nDEF")]        // no trailing newline on the last line
        public async Task ReadAsync_Pipe_HandlesLineEndingsAndTrailingNewline(string text)
        {
            var reader = new FixedWidthByteReader<CodeModel>();

            var codes = (await CollectAsync(reader.ReadAsync(Pipe(text))).ConfigureAwait(true)).Select(m => m.Code).ToList();

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public async Task ReadAsync_Pipe_SkipsEmptyLines()
        {
            var reader = new FixedWidthByteReader<CodeModel>();

            var codes = (await CollectAsync(reader.ReadAsync(Pipe("ABC\n\n\nDEF\n"))).ConfigureAwait(true)).Select(m => m.Code).ToList();

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public async Task ReadAsync_Pipe_SkipsLeadingBom()
        {
            var reader = new FixedWidthByteReader<CodeModel>();

            var codes = (await CollectAsync(reader.ReadAsync(Pipe("﻿ABC\nDEF\n"))).ConfigureAwait(true)).Select(m => m.Code).ToList();

            Assert.Equal(["ABC", "DEF"], codes);
        }

        [Fact]
        public async Task ReadAsync_Pipe_MultiSegmentLine_ParsesViaScratch()
        {
            // A tiny pipe buffer (8 bytes) forces each 25-byte record to span several segments, so the
            // line is non-contiguous and must be copied into the pooled scratch buffer before parsing.
            var reader = new FixedWidthByteReader<PersonModel>(Inv);

            var people = await CollectAsync(reader.ReadAsync(Pipe(TwoPeople, bufferSize: 8))).ConfigureAwait(true);

            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal(60000.00, people[0].Salary, 2);
            Assert.Equal("Jane", people[1].Name);
            Assert.Equal(28, people[1].Age);
        }

        [Fact]
        public async Task ReadAsync_Pipe_MatchesStreamReader()
        {
            // Same content, two sources: the pipe path must produce exactly what the stream path does.
            var byteReader = new FixedWidthByteReader<PersonModel>(Inv);

#pragma warning disable IDISP004 // the reader owns and disposes the stream when iteration completes
            var fromStream = await CollectAsync(byteReader.ReadAsync(StreamOf(TwoPeople))).ConfigureAwait(true);
#pragma warning restore IDISP004
            var fromPipe = await CollectAsync(byteReader.ReadAsync(Pipe(TwoPeople))).ConfigureAwait(true);

            Assert.Equal(fromStream.Count, fromPipe.Count);
            for (int i = 0; i < fromStream.Count; i++)
            {
                Assert.Equal(fromStream[i].Name, fromPipe[i].Name);
                Assert.Equal(fromStream[i].Age, fromPipe[i].Age);
                Assert.Equal(fromStream[i].Salary, fromPipe[i].Salary, 2);
            }
        }

        [Fact]
        public async Task ReadAsync_Pipe_HonorsCancellation()
        {
            var reader = new FixedWidthByteReader<CodeModel>();
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync().ConfigureAwait(true);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in reader.ReadAsync(Pipe("ABC\nDEF\n")).WithCancellation(cts.Token).ConfigureAwait(false))
                {
                    Assert.Fail("Enumeration should have been cancelled before yielding a record.");
                }
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task ReadAsync_Pipe_CompletesReaderByDefault()
        {
            var reader = new FixedWidthByteReader<CodeModel>();
            var pipe = Pipe("ABC\nDEF\n");

            _ = await CollectAsync(reader.ReadAsync(pipe)).ConfigureAwait(true);

            // The reader was completed, so a further read is no longer allowed.
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await pipe.ReadAsync().ConfigureAwait(false)).ConfigureAwait(true);
        }

        [Fact]
        public async Task ReadAsync_Pipe_LeaveOpen_DoesNotCompleteReader()
        {
            var reader = new FixedWidthByteReader<CodeModel>();
            var pipe = Pipe("ABC\nDEF\n");

            _ = await CollectAsync(reader.ReadAsync(pipe, leaveOpen: true)).ConfigureAwait(true);

            // Not completed: a further read succeeds and reports end-of-data.
            var result = await pipe.ReadAsync().ConfigureAwait(true);
            Assert.True(result.IsCompleted);
            pipe.AdvanceTo(result.Buffer.End);
            await pipe.CompleteAsync().ConfigureAwait(true);
        }

        [Fact]
        public void ReadAsync_Pipe_NullReader_Throws()
        {
            var reader = new FixedWidthByteReader<CodeModel>();

            Assert.Throws<ArgumentNullException>(() => reader.ReadAsync((PipeReader)null!));
        }
    }
}
