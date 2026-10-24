using System.Text;
using Apache.Arrow;
using Apache.Arrow.C;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using FixedWidthParser.Native;
using FixedWidthParser.Native.Arrow;
using FixedWidthParser.Native.Layouts;
using FixedWidthParser.Native.Reading;
using static FixedWidthParser.Tests.Native.NativeTestLayouts;
using static FixedWidthParser.Tests.Native.NativeTestReads;

namespace FixedWidthParser.Tests.Native
{
    public sealed class NativeArrowTests
    {
        private const string Content =
            "Ann   " + "00042" + "  12.500" + "00012345" + "20231024" + "133005" + "20231024133000" + "\n" +
            "      " + "     " + "    -0.5" + "-0000001" + "19700101" + "000000" + "19700101000000" + "\n" +
            "Cid   " + "00007" + "   3.250" + "00000100" + "20000229" + "235959" + "20000229235959" + "\n";

        private static readonly int ReaderOptionsSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeReaderOptionsRaw>();

        private static NativeLayout Layout()
        {
            return Build(
                new Col("name", 0, 6, TString) { Nullable = true },
                new Col("qty", 6, 5, TInt64) { Nullable = true },
                new Col("price", 11, 8, TFloat64),
                new Col("amount", 19, 8, TDecimal) { Scale = 2 },
                new Col("day", 27, 8, TDate) { Format = "yyyyMMdd" },
                new Col("time", 35, 6, TTime) { Format = "HHmmss" },
                new Col("stamp", 41, 14, TTimestamp) { Format = "yyyyMMddHHmmss" });
        }

        private static unsafe RecordBatch ReadBatch(string content, NativeLayout layout, NativeReadOptionsRaw? options = null)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            ArrowArray array = default;
            ArrowSchema schema = default;
            NativeReadOptionsRaw value = options ?? default;
            fixed (byte* data = bytes)
            {
                int status = ArrowApi.ReadMemory(data, bytes.Length, layout, options is null ? null : &value, &array, &schema, null);
                Assert.True(status == NativeStatus.Ok, LastError());
            }
            var imported = CArrowSchemaImporter.ImportSchema((CArrowSchema*)&schema);
            return CArrowArrayImporter.ImportRecordBatch((CArrowArray*)&array, imported);
        }

        private static object?[] Values(IArrowArray column)
        {
            return column switch
            {
                StringArray s => [.. Enumerable.Range(0, s.Length).Select(i => s.IsNull(i) ? null : s.GetString(i))],
                Int64Array l => [.. Enumerable.Range(0, l.Length).Select(i => (object?)l.GetValue(i))],
                DoubleArray d => [.. Enumerable.Range(0, d.Length).Select(i => (object?)d.GetValue(i))],
                Decimal128Array m => [.. Enumerable.Range(0, m.Length).Select(i => (object?)m.GetValue(i))],
                Date32Array t => [.. Enumerable.Range(0, t.Length).Select(i => (object?)t.GetValue(i))],
                Time64Array t => [.. Enumerable.Range(0, t.Length).Select(i => (object?)t.GetValue(i))],
                TimestampArray t => [.. Enumerable.Range(0, t.Length).Select(i => (object?)t.GetValue(i))],
                _ => throw new InvalidOperationException(column.GetType().Name),
            };
        }

        [Fact]
        public void Arrow_EveryType_MatchesColumnarRead()
        {
            var layout = Layout();
            var columnar = FromMemory(Encoding.UTF8.GetBytes(Content), layout);

            using var batch = ReadBatch(Content, layout);

            Assert.Equal(3, batch.Length);
            Assert.Equal(["name", "qty", "price", "amount", "day", "time", "stamp"], batch.Schema.FieldsList.Select(f => f.Name));
            Assert.Equal(columnar.Column(0), Values(batch.Column(0)));
            Assert.Equal(columnar.Column(1), Values(batch.Column(1)));
            Assert.Equal(columnar.Column(2), Values(batch.Column(2)));
            Assert.Equal([123.45m, -0.01m, 1.00m], Values(batch.Column(3)));
            Assert.Equal(columnar.Column(4), Values(batch.Column(4)));
            Assert.Equal(columnar.Column(5), Values(batch.Column(5)));
            Assert.Equal(columnar.Column(6), Values(batch.Column(6)));
        }

        [Fact]
        public void Arrow_Nulls_HaveValidity()
        {
            using var batch = ReadBatch(Content, Layout());

            Assert.Equal(1, batch.Column(0).NullCount);
            Assert.True(batch.Column(1).IsNull(1));
            Assert.Equal(0, batch.Column(2).NullCount);
            Assert.True(batch.Schema.GetFieldByName("name").IsNullable);
            Assert.False(batch.Schema.GetFieldByName("price").IsNullable);
        }

        [Fact]
        public void Arrow_Decimal_HasScale()
        {
            using var batch = ReadBatch(Content, Layout());

            var type = Assert.IsType<Decimal128Type>(batch.Schema.GetFieldByName("amount").DataType);
            Assert.Equal(18, type.Precision);
            Assert.Equal(2, type.Scale);
            Assert.IsType<TimestampType>(batch.Schema.GetFieldByName("stamp").DataType);
            Assert.Equal(TimeUnit.Microsecond, ((TimestampType)batch.Schema.GetFieldByName("stamp").DataType).Unit);
        }

        [Fact]
        public void Arrow_Parallel_EqualsSequential()
        {
            var layout = Layout();
            string content = string.Concat(Enumerable.Repeat(Content, 50));

            using var sequential = ReadBatch(content, layout, Options(degreeOfParallelism: 1));
            using var parallel = ReadBatch(content, layout, Options(degreeOfParallelism: 4));

            for (int i = 0; i < 7; i++)
            {
                Assert.Equal(Values(sequential.Column(i)), Values(parallel.Column(i)));
            }
        }

        [Fact]
        public async Task ArrowStream_BatchesConcatenate_ToTheWholeRead()
        {
            var layout = Layout();
            string content = string.Concat(Enumerable.Repeat(Content, 5));
            using var whole = ReadBatch(content, layout);
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            var options = new NativeReaderOptionsRaw { StructSize = ReaderOptionsSize, BatchRows = 4 };

            var batches = new List<RecordBatch>();
            IArrowArrayStream stream = OpenStream(bytes, layout, options);
            try
            {
                while (await stream.ReadNextRecordBatchAsync() is { } batch)
                {
                    batches.Add(batch);
                }
            }
            finally
            {
                stream.Dispose();
            }

            Assert.Equal([4, 4, 4, 3], batches.Select(b => b.Length));
            for (int i = 0; i < 7; i++)
            {
                Assert.Equal(Values(whole.Column(i)), batches.SelectMany(b => Values(b.Column(i))));
            }
            batches.ForEach(b => b.Dispose());
        }

        [Fact]
        public async Task ArrowStream_ParseError_IsReported()
        {
            var layout = Build(new Col("n", 0, 2, TInt64));
            byte[] bytes = "01\nXX\n"u8.ToArray();

            IArrowArrayStream stream = OpenStream(bytes, layout, new NativeReaderOptionsRaw { StructSize = ReaderOptionsSize, BatchRows = 1 });
            try
            {
                using var first = await stream.ReadNextRecordBatchAsync();
                var ex = await Assert.ThrowsAnyAsync<Exception>(async () => await stream.ReadNextRecordBatchAsync());
                Assert.Contains("Line 2", ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                stream.Dispose();
            }
        }

        [Fact]
        public unsafe void Arrow_ParseError_IsReported()
        {
            var layout = Build(new Col("n", 0, 2, TInt64));
            ArrowArray array = default;
            ArrowSchema schema = default;
            fixed (byte* data = "XX\n"u8)
            {
                Assert.Equal(NativeStatus.ParseError, ArrowApi.ReadMemory(data, 3, layout, null, &array, &schema, null));
            }
            Assert.Equal(IntPtr.Zero, array.Release);
            Assert.Equal(IntPtr.Zero, schema.Release);
        }

        [Fact]
        public unsafe void Arrow_Release_IsIdempotent()
        {
            byte[] bytes = Encoding.UTF8.GetBytes(Content);
            ArrowArray array = default;
            ArrowSchema schema = default;
            fixed (byte* data = bytes)
            {
                Assert.Equal(NativeStatus.Ok, ArrowApi.ReadMemory(data, bytes.Length, Layout(), null, &array, &schema, null));
            }

            ArrowApi.ReleaseArrowArray((IntPtr)(&array));
            ArrowApi.ReleaseArrowArray((IntPtr)(&array));
            ArrowApi.ReleaseArrowSchema((IntPtr)(&schema));
            ArrowApi.ReleaseArrowSchema((IntPtr)(&schema));

            Assert.Equal(IntPtr.Zero, array.Release);
            Assert.Equal(IntPtr.Zero, schema.Release);
        }

        private static unsafe IArrowArrayStream OpenStream(byte[] bytes, NativeLayout layout, NativeReaderOptionsRaw options)
        {
            var stream = (CArrowArrayStream*)System.Runtime.InteropServices.NativeMemory.AllocZeroed((nuint)sizeof(CArrowArrayStream));
            fixed (byte* data = bytes)
            {
                // The stream reads the memory lazily, so it is copied to memory that outlives this call.
                byte* copy = (byte*)System.Runtime.InteropServices.NativeMemory.Alloc((nuint)bytes.Length);
                new ReadOnlySpan<byte>(data, bytes.Length).CopyTo(new Span<byte>(copy, bytes.Length));
                Assert.Equal(NativeStatus.Ok, ArrowApi.OpenStreamMemory(copy, bytes.Length, layout, &options, (ArrowArrayStream*)stream));
            }
            return CArrowArrayStreamImporter.ImportArrayStream(stream);
        }
    }
}
