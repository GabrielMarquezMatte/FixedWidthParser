using System.Runtime.InteropServices;
using System.Text;
using Apache.Arrow;
using Apache.Arrow.C;
using FixedWidthParser.Native;
using FixedWidthParser.Native.Arrow;
using FixedWidthParser.Native.Layouts;
using FixedWidthParser.Native.Reading;
using static FixedWidthParser.Tests.Native.NativeTestLayouts;
using static FixedWidthParser.Tests.Native.NativeTestReads;

namespace FixedWidthParser.Tests.Native
{
    /// <summary>
    /// <c>fw_reader_open_stream</c> and <c>fw_read_arrow_stream_stream</c> over a real <c>fw_stream</c> whose callbacks
    /// hand out a few bytes at a time: the batches equal a read of the same bytes from memory, header lines are skipped
    /// without seeking, a failing callback's message reaches the caller, and <c>release</c> runs exactly once.
    /// </summary>
    public sealed unsafe class NativeStreamReaderTests
    {
        private static readonly byte[] Content = Encoding.UTF8.GetBytes(
            "HEADER\n" + string.Concat(Enumerable.Range(1, 10).Select(i => i == 5 ? "ZZXX\r\n" : $"AB{i:D2}\n")) + "AB11");

        /// <summary>What the callbacks read: at most <see cref="Chunk"/> bytes per call, failing at <see cref="FailAt"/>.</summary>
        private sealed class Source(byte[] data, int chunk, int failAt = -1, long overReport = 0)
        {
            public int Position;
            public int Released;

            public long Read(byte* buffer, long length)
            {
                if (failAt >= 0 && Position >= failAt)
                {
                    byte[] message = "the disk went away"u8.ToArray();
                    fixed (byte* m = message)
                    {
                        SourceErrors.Set(Encoding.UTF8.GetString(m, message.Length));
                    }
                    return -1;
                }
                int count = (int)Math.Min(Math.Min(length, Chunk), data.Length - Position);
                data.AsSpan(Position, count).CopyTo(new Span<byte>(buffer, count));
                Position += count;
                return overReport > 0 ? length + overReport : count;
            }

            public int Chunk => chunk;
        }

        [UnmanagedCallersOnly]
        private static long ReadCallback(void* userData, byte* buffer, long length)
        {
            return ((Source)GCHandle.FromIntPtr((IntPtr)userData).Target!).Read(buffer, length);
        }

        [UnmanagedCallersOnly]
        private static void ReleaseCallback(void* userData)
        {
            ((Source)GCHandle.FromIntPtr((IntPtr)userData).Target!).Released++;
        }

        /// <summary>An <c>fw_stream</c> over <paramref name="source"/>; the handle keeps it alive for the test.</summary>
        private static CallbackReadStream Wrap(Source source, GCHandle handle)
        {
            var raw = new NativeStreamRaw
            {
                StructSize = sizeof(NativeStreamRaw),
                UserData = (void*)GCHandle.ToIntPtr(handle),
                Read = &ReadCallback,
                Release = &ReleaseCallback,
            };
            Assert.True(CallbackReadStream.TryCreate(&raw, "test", out var stream));
            return stream;
        }

        private static NativeLayout Layout()
        {
            return Build(new Col("code", 0, 2, TString), new Col("n", 2, 2, TInt64));
        }

        private static NativeReaderOptionsRaw ReaderOptions(long batchRows, long headerLines = 1, bool skipInvalid = true)
        {
            return new NativeReaderOptionsRaw
            {
                StructSize = sizeof(NativeReaderOptionsRaw),
                Read = Options(headerLines: (int)headerLines, skipInvalid: skipInvalid),
                BatchRows = batchRows,
            };
        }

        private static List<ReadOutcome> Drain(nint reader, out int lastStatus)
        {
            var batches = new List<ReadOutcome>();
            while (true)
            {
                NativeTableRaw table = default;
                lastStatus = ReaderApi.Next(reader, &table);
                if (lastStatus != NativeStatus.Ok)
                {
                    return batches;
                }
                batches.Add(Collect(lastStatus, &table, default));
            }
        }

        [Theory]
        [InlineData(1, 3)]
        [InlineData(7, 4)]
        [InlineData(4096, 1000)]
        public void Stream_EqualsMemory_AndReleasesOnce(int chunk, long batchRows)
        {
            var layout = Layout();
            var whole = FromMemory(Content, layout, Options(headerLines: 1, skipInvalid: true));
            var source = new Source(Content, chunk);
            var handle = GCHandle.Alloc(source);
            try
            {
                var options = ReaderOptions(batchRows);
                Assert.Equal(NativeStatus.Ok, ReaderApi.OpenStream(Wrap(source, handle), layout, &options, out nint reader));
                var batches = Drain(reader, out int last);
                Assert.Equal(0, source.Released);
                ReaderApi.Close(reader);

                Assert.Equal(NativeStatus.Eof, last);
                Assert.Equal(whole.Column(0), batches.SelectMany(b => b.Column(0)));
                Assert.Equal(whole.Column(1), batches.SelectMany(b => b.Column(1)));
                Assert.Equal(1, source.Released);
            }
            finally
            {
                handle.Free();
            }
        }

        [Theory]
        [InlineData("﻿AB01\nAB02\n", 0)]
        [InlineData("﻿HEAD\nAB01\nAB02", 1)]
        [InlineData("H1\r\n\nAB01\n", 2)]
        [InlineData("ONLY A HEADER", 3)]
        public void HeaderLinesAndByteOrderMark_MatchMemory(string text, long headerLines)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            var layout = Layout();
            var expected = FromMemory(bytes, layout, Options(headerLines: (int)headerLines));
            var source = new Source(bytes, 1);
            var handle = GCHandle.Alloc(source);
            try
            {
                var options = ReaderOptions(100, headerLines, skipInvalid: false);
                Assert.Equal(NativeStatus.Ok, ReaderApi.OpenStream(Wrap(source, handle), layout, &options, out nint reader));
                var batches = Drain(reader, out int last);
                ReaderApi.Close(reader);

                Assert.True(last == NativeStatus.Eof, LastError());
                Assert.Equal(expected.Column(1), batches.SelectMany(b => b.Column(1)));
            }
            finally
            {
                handle.Free();
            }
        }

        [Fact]
        public void ParseError_CountsTheSkippedHeaderLines()
        {
            var source = new Source(Content, 5);
            var handle = GCHandle.Alloc(source);
            try
            {
                var options = ReaderOptions(100, skipInvalid: false);
                Assert.Equal(NativeStatus.Ok, ReaderApi.OpenStream(Wrap(source, handle), Layout(), &options, out nint reader));
                Drain(reader, out int last);
                NativeApi.LastParseError(out long line, out _);
                ReaderApi.Close(reader);

                Assert.Equal(NativeStatus.ParseError, last);
                Assert.Equal(6, line);
            }
            finally
            {
                handle.Free();
            }
        }

        [Fact]
        public void FailingRead_ReportsTheCallbacksMessage()
        {
            var source = new Source(Content, 8, failAt: 30);
            var handle = GCHandle.Alloc(source);
            try
            {
                var options = ReaderOptions(2);
                Assert.Equal(NativeStatus.Ok, ReaderApi.OpenStream(Wrap(source, handle), Layout(), &options, out nint reader));
                Drain(reader, out int last);

                Assert.Equal(NativeStatus.Error, last);
                Assert.Equal("the disk went away", LastError());
                ReaderApi.Close(reader);
                Assert.Equal(1, source.Released);
            }
            finally
            {
                handle.Free();
            }
        }

        [Fact]
        public void ReadReturningMoreThanAsked_IsAnError()
        {
            var source = new Source(Content, 4096, overReport: 1);
            var handle = GCHandle.Alloc(source);
            try
            {
                var options = ReaderOptions(100, headerLines: 0);
                Assert.Equal(NativeStatus.Ok, ReaderApi.OpenStream(Wrap(source, handle), Layout(), &options, out nint reader));
                Drain(reader, out int last);
                ReaderApi.Close(reader);

                Assert.Equal(NativeStatus.Error, last);
                Assert.Contains("fw_stream.read returned", LastError(), StringComparison.Ordinal);
            }
            finally
            {
                handle.Free();
            }
        }

        [Fact]
        public void BadOptions_ReleaseTheStream()
        {
            var source = new Source(Content, 16);
            var handle = GCHandle.Alloc(source);
            try
            {
                var options = new NativeReaderOptionsRaw { StructSize = 3 };
                Assert.Equal(NativeStatus.InvalidArgument, ReaderApi.OpenStream(Wrap(source, handle), Layout(), &options, out nint reader));

                Assert.Equal(0, reader);
                Assert.Equal(1, source.Released);
            }
            finally
            {
                handle.Free();
            }
        }

        [Fact]
        public void RejectedStruct_IsNotReleased()
        {
            var raw = new NativeStreamRaw { StructSize = 0, Read = &ReadCallback, Release = &ReleaseCallback };
            Assert.False(CallbackReadStream.TryCreate(&raw, "fw_reader_open_stream", out _));
            Assert.Contains("struct_size is sizeof(fw_stream)", LastError(), StringComparison.Ordinal);

            raw = new NativeStreamRaw { StructSize = sizeof(NativeStreamRaw), Release = &ReleaseCallback };
            Assert.False(CallbackReadStream.TryCreate(&raw, "fw_reader_open_stream", out _));
        }

        [Fact]
        public void ArrowStream_OverAStream_EqualsMemory_AndReleasesWithIt()
        {
            var layout = Layout();
            var whole = FromMemory(Content, layout, Options(headerLines: 1, skipInvalid: true));
            var source = new Source(Content, 3);
            var handle = GCHandle.Alloc(source);
            try
            {
                var stream = (CArrowArrayStream*)NativeMemory.AllocZeroed((nuint)sizeof(CArrowArrayStream));
                var options = ReaderOptions(4);
                Assert.Equal(NativeStatus.Ok, ArrowApi.OpenStreamStream(Wrap(source, handle), layout, &options, (ArrowArrayStream*)stream));
                var imported = CArrowArrayStreamImporter.ImportArrayStream(stream);
                var quantities = new List<long?>();
                try
                {
                    // An unsafe context cannot await; the native stream completes every read synchronously.
                    while (imported.ReadNextRecordBatchAsync(TestContext.Current.CancellationToken).AsTask().GetAwaiter().GetResult() is { } batch)
                    {
                        using (batch)
                        {
                            var column = (Int64Array)batch.Column(1);
                            quantities.AddRange(Enumerable.Range(0, column.Length).Select(i => column.GetValue(i)));
                        }
                    }
                }
                finally
                {
                    imported.Dispose();
                }

                Assert.Equal(whole.Column(1), quantities.Cast<object?>());
                Assert.Equal(1, source.Released);
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
