using System.Text;
using FixedWidthParser.Native;
using FixedWidthParser.Native.Layouts;
using FixedWidthParser.Native.Reading;
using static FixedWidthParser.Tests.Native.NativeTestLayouts;
using static FixedWidthParser.Tests.Native.NativeTestReads;

namespace FixedWidthParser.Tests.Native
{
    public sealed unsafe class NativeReaderTests
    {
        private static readonly byte[] Content = Encoding.UTF8.GetBytes(
            "HEADER\n" + string.Concat(Enumerable.Range(1, 10).Select(i => i == 5 ? "ZZXX\r\n" : $"AB{i:D2}\n")) + "AB11");

        private static NativeLayout Layout()
        {
            return Build(new Col("code", 0, 2, TString), new Col("n", 2, 2, TInt64));
        }

        private static NativeReaderOptionsRaw ReaderOptions(long batchRows, bool skipInvalid = true)
        {
            return new NativeReaderOptionsRaw
            {
                StructSize = sizeof(NativeReaderOptionsRaw),
                Read = Options(headerLines: 1, skipInvalid: skipInvalid),
                BatchRows = batchRows,
            };
        }

        private static nint Open(NativeLayout layout, NativeReaderOptionsRaw options, byte* data)
        {
            Assert.Equal(NativeStatus.Ok, ReaderApi.OpenMemory(data, Content.Length, layout, &options, out nint reader));
            return reader;
        }

        private static List<ReadOutcome> Drain(nint reader)
        {
            var batches = new List<ReadOutcome>();
            while (true)
            {
                NativeTableRaw table = default;
                int status = ReaderApi.Next(reader, &table);
                if (status == NativeStatus.Eof)
                {
                    return batches;
                }
                Assert.True(status == NativeStatus.Ok, LastError());
                batches.Add(Collect(status, &table, default));
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(1000)]
        public void Reader_BatchesConcatenate_ToTheWholeRead(long batchRows)
        {
            var layout = Layout();
            var whole = FromMemory(Content, layout, Options(headerLines: 1, skipInvalid: true));

            fixed (byte* data = Content)
            {
                nint reader = Open(layout, ReaderOptions(batchRows), data);
                var batches = Drain(reader);
                ReaderApi.Close(reader);

                Assert.All(batches, b => Assert.InRange(b.RowCount, 1, batchRows));
                Assert.Equal(whole.Column(0), batches.SelectMany(b => b.Column(0)));
                Assert.Equal(whole.Column(1), batches.SelectMany(b => b.Column(1)));
                Assert.Equal(10, batches.Sum(b => b.RowCount));
            }
        }

        [Fact]
        public void Reader_File_EqualsMemory()
        {
            var layout = Layout();
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(path, Content);
                var options = ReaderOptions(4);
                Assert.Equal(NativeStatus.Ok, ReaderApi.OpenFile(Encoding.UTF8.GetBytes(path), layout, &options, out nint reader));
                var batches = Drain(reader);
                ReaderApi.Close(reader);

                Assert.Equal([4L, 4L, 2L], batches.Select(b => b.RowCount));
                Assert.Equal(FromMemory(Content, layout, Options(headerLines: 1, skipInvalid: true)).Column(1), batches.SelectMany(b => b.Column(1)));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Reader_AfterEof_ReturnsEof()
        {
            fixed (byte* data = Content)
            {
                nint reader = Open(Layout(), ReaderOptions(100), data);
                Drain(reader);
                NativeTableRaw table = default;

                Assert.Equal(NativeStatus.Eof, ReaderApi.Next(reader, &table));
                Assert.Equal(0, table.ColumnCount);
                ReaderApi.Close(reader);
            }
        }

        [Fact]
        public void Reader_ParseError_StopsWithLineNumber()
        {
            fixed (byte* data = Content)
            {
                nint reader = Open(Layout(), ReaderOptions(2, skipInvalid: false), data);
                NativeTableRaw table = default;

                Assert.Equal(NativeStatus.Ok, ReaderApi.Next(reader, &table));
                NativeTable.Free(&table);
                Assert.Equal(NativeStatus.Ok, ReaderApi.Next(reader, &table));
                NativeTable.Free(&table);
                Assert.Equal(NativeStatus.ParseError, ReaderApi.Next(reader, &table));
                NativeApi.LastParseError(out long line, out int column);
                Assert.Equal(6, line);
                Assert.Equal(1, column);
                Assert.Equal(NativeStatus.Eof, ReaderApi.Next(reader, &table));
                ReaderApi.Close(reader);
            }
        }

        [Fact]
        public void Reader_Closed_IsInvalidHandle()
        {
            fixed (byte* data = Content)
            {
                nint reader = Open(Layout(), ReaderOptions(2), data);
                ReaderApi.Close(reader);
                NativeTableRaw table = default;

                Assert.Equal(NativeStatus.InvalidHandle, ReaderApi.Next(reader, &table));
            }
        }

        [Fact]
        public void Reader_ZeroedOptions_UseDefaults()
        {
            var options = new NativeReaderOptionsRaw { StructSize = sizeof(NativeReaderOptionsRaw) };
            fixed (byte* data = "AB01\nAB02\n"u8)
            {
                Assert.Equal(NativeStatus.Ok, ReaderApi.OpenMemory(data, 10, Layout(), &options, out nint reader));
                var batches = Drain(reader);
                ReaderApi.Close(reader);

                Assert.Equal([2L], batches.Select(b => b.RowCount));
            }
        }
    }
}
