using System.Text;
using FixedWidthParser.Native;
using FixedWidthParser.Native.Arrow;
using FixedWidthParser.Native.Reading;
using static FixedWidthParser.Tests.Native.NativeTestLayouts;
using static FixedWidthParser.Tests.Native.NativeTestReads;

namespace FixedWidthParser.Tests.Native
{
    /// <summary>The size limits of one table, exercised with small limits instead of gigabytes of input.</summary>
    public sealed unsafe class NativeLimitsTests
    {
        private static int ReadWith(byte[] content, TableLimits limits, int degree, int chunkSize, out NativeTableRaw table)
        {
            var options = Options(degreeOfParallelism: degree);
            NativeTableRaw result = default;
            int status;
            fixed (byte* data = content)
            {
                status = ReadApi.ReadMemory(data, content.Length, Build(new Col("s", 0, 4, TString)), &options, &result, null, chunkSize, limits);
            }
            table = result;
            return status;
        }

        [Fact]
        public void PartitionedText_OverTheLimit_FailsBeforePublishing()
        {
            byte[] content = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("abcd\n", 10)));

            int status = ReadWith(content, new TableLimits(MaxTextBytes: 30, MaxRows: int.MaxValue), degree: 4, chunkSize: 5, out var table);

            Assert.Equal(NativeStatus.Error, status);
            Assert.Contains("more than 30 bytes of text", LastError(), StringComparison.Ordinal);
            Assert.True(table.Columns is null);
        }

        [Fact]
        public void SequentialText_OverTheLimit_Fails()
        {
            byte[] content = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("abcd\n", 10)));

            Assert.Equal(NativeStatus.Error, ReadWith(content, new TableLimits(MaxTextBytes: 30, MaxRows: int.MaxValue), degree: 1, chunkSize: 0, out _));
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(4, 5)]
        public void Rows_OverTheLimit_Fail(int degree, int chunkSize)
        {
            byte[] content = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("abcd\n", 10)));

            int status = ReadWith(content, new TableLimits(MaxTextBytes: int.MaxValue, MaxRows: 4), degree, chunkSize, out var table);

            Assert.Equal(NativeStatus.Error, status);
            Assert.Contains("more than 4 rows", LastError(), StringComparison.Ordinal);
            Assert.True(table.Columns is null);
        }

        [Fact]
        public void BufferGrowth_PastHalfOfInt_ClampsInsteadOfOverflowing()
        {
            Assert.Equal(int.MaxValue, GrowableNativeBuffer<byte>.NextCapacity(1 << 30, (1L << 30) + 1));
            Assert.Equal(512, GrowableNativeBuffer<byte>.NextCapacity(256, 257));
            Assert.Throws<InvalidOperationException>(() => GrowableNativeBuffer<byte>.NextCapacity(int.MaxValue, (long)int.MaxValue + 1));
        }

        // A TableArena reserves a STRING column's full width on every row; trimmed text often uses a sliver of it.
        [Fact]
        public unsafe void TrimExcess_ShrinksOnlyAnOwnedMostlyEmptyBlock()
        {
            using var sparse = new GrowableNativeBuffer<byte>();
            sparse.Reserve(1000);
            "OK"u8.CopyTo(sparse.Append(2));
            sparse.TrimExcess(2);
            Assert.Equal(2, sparse.Capacity);
            Assert.True(sparse.Span.SequenceEqual("OK"u8));

            using var dense = new GrowableNativeBuffer<byte>();
            dense.Reserve(1000);
            dense.Append(600);
            dense.TrimExcess(600);
            Assert.Equal(1000, dense.Capacity);

            byte* lent = stackalloc byte[1000];
            using var borrowed = new GrowableNativeBuffer<byte>();
            borrowed.Borrow(lent, 1000);
            borrowed.TrimExcess(2);
            Assert.Equal(1000, borrowed.Capacity);
            Assert.True(borrowed.Pointer == lent);
        }

        [Fact]
        public void DecimalBuffer_SizeIsComputedIn64Bits()
        {
            Assert.Equal(4_294_967_312L, ArrowApi.DecimalBufferBytes(268_435_457));
            Assert.Equal(16L, ArrowApi.DecimalBufferBytes(0));
        }

        [Fact]
        public void Reader_BatchRowsOverInt_IsInvalidArgument()
        {
            var options = new NativeReaderOptionsRaw { StructSize = sizeof(NativeReaderOptionsRaw), BatchRows = (long)int.MaxValue + 1 };
            fixed (byte* data = "abcd\n"u8)
            {
                Assert.Equal(NativeStatus.InvalidArgument, ReaderApi.OpenMemory(data, 5, Build(new Col("s", 0, 4, TString)), &options, out _));
            }
        }
    }
}
