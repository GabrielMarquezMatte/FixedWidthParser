using System.Text;
using FixedWidthParser.Readers;

namespace FixedWidthParser.Tests
{
    /// <summary>The internal parallel read over a line sink and a random-access source, used by the native layer.</summary>
    public sealed class LineSinkTests : IDisposable
    {
        private readonly string _path = Path.GetTempFileName();

        public void Dispose()
        {
            File.Delete(_path);
        }

        private struct Lines : ILineSink<Lines>
        {
            private List<string>? _lines;

            internal readonly List<string> Values => _lines ?? [];

            public bool TryAdd(ReadOnlySpan<byte> line, in FixedWidthContext context)
            {
                if (line.SequenceEqual("XX"u8))
                {
                    return false;
                }
                (_lines ??= []).Add(Encoding.UTF8.GetString(line));
                return true;
            }

            public readonly FixedWidthParseException Diagnose(ReadOnlySpan<byte> line, long lineNumber, in FixedWidthContext context)
            {
                return new FixedWidthParseException("rejected", lineNumber, columnName: null, columnStart: -1, columnLength: 0, rawValue: null);
            }

            public void Merge(Lines following)
            {
                (_lines ??= []).AddRange(following.Values);
            }
        }

        private sealed class ArraySource(byte[] bytes) : IRandomAccessSource
        {
            public long Length => bytes.Length;

            public int Read(Span<byte> destination, long offset)
            {
                var rest = bytes.AsSpan((int)Math.Min(offset, bytes.Length));
                int count = Math.Min(rest.Length, destination.Length);
                rest[..count].CopyTo(destination);
                return count;
            }
        }

        private static FixedWidthReaderOptions Options(int chunkSize)
        {
            return FixedWidthReaderOptions.Validate(new FixedWidthReaderOptions { DegreeOfParallelism = 4, ChunkSize = chunkSize });
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(64)]
        public async Task RunAsync_FromDataStart_SkipsEarlierBytes(int chunkSize)
        {
            var source = new ArraySource("HDR\nA1\nA2\n"u8.ToArray());

            var lines = await ParallelAggregator.RunAsync(source, dataStart: 4, (_, _) => new Lines(), Options(chunkSize), CancellationToken.None);

            Assert.Equal(["A1", "A2"], lines.Values);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(64)]
        public async Task RunAsync_ErrorLineNumber_CountsFromSourceStart(int chunkSize)
        {
            var source = new ArraySource("HDR\nA1\nXX\nA3\n"u8.ToArray());

            var ex = await Assert.ThrowsAsync<FixedWidthParseException>(() =>
                ParallelAggregator.RunAsync(source, dataStart: 4, (_, _) => new Lines(), Options(chunkSize), CancellationToken.None));

            Assert.Equal(3, ex.LineNumber);
        }

        [Fact]
        public async Task RunAsync_MemorySource_EqualsFileSource()
        {
            byte[] content = "﻿A1\r\nA2\n\nA3"u8.ToArray();
            await File.WriteAllBytesAsync(_path, content);
            using var handle = File.OpenHandle(_path);

            var fromFile = await ParallelAggregator.RunAsync(new FileSource(handle), 0, (_, _) => new Lines(), Options(3), CancellationToken.None);
            var fromMemory = await ParallelAggregator.RunAsync(new ArraySource(content), 0, (_, _) => new Lines(), Options(3), CancellationToken.None);

            Assert.Equal(["A1", "A2", "A3"], fromFile.Values);
            Assert.Equal(fromFile.Values, fromMemory.Values);
        }
    }
}
