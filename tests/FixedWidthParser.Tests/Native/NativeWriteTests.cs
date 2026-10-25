using System.Runtime.InteropServices;
using System.Text;
using FixedWidthParser.Native;
using FixedWidthParser.Native.Layouts;
using FixedWidthParser.Native.Reading;
using FixedWidthParser.Native.Writing;
using static FixedWidthParser.Tests.Native.NativeTestLayouts;
using static FixedWidthParser.Tests.Native.NativeTestReads;

namespace FixedWidthParser.Tests.Native
{
    public sealed unsafe class NativeWriteTests
    {
        private const string EveryType =
            "Ann   " + "00042" + "  12.500" + "00012345" + "20231024" + "133005" + "20231024133000" + "\n" +
            "      " + "     " + "    -0.5" + "-0000001" + "19700101" + "000000" + "19700101000000" + "\n";

        private static NativeLayout EveryTypeLayout()
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

        private static NativeWriteOptionsRaw WriteOptions(int degree = 1, long batchRows = 0, int encoding = 0)
        {
            return new NativeWriteOptionsRaw { StructSize = sizeof(NativeWriteOptionsRaw), DegreeOfParallelism = degree, BatchRows = batchRows, Encoding = encoding };
        }

        /// <summary>Reads <paramref name="content"/> into a table the caller frees.</summary>
        private static NativeTableRaw ReadTable(byte[] content, NativeLayout layout, NativeReadOptionsRaw? options = null)
        {
            NativeTableRaw table = default;
            NativeReadOptionsRaw value = options ?? default;
            fixed (byte* data = content)
            {
                int status = ReadApi.ReadMemory(data, content.Length, layout, options is null ? null : &value, &table, null);
                Assert.True(status == NativeStatus.Ok, LastError());
            }
            return table;
        }

        private static (int Status, byte[] Bytes, string Error) WriteMemory(NativeTableRaw* table, NativeLayout layout, NativeWriteOptionsRaw? options = null)
        {
            NativeWriteOptionsRaw value = options ?? default;
            NativeBufferRaw buffer = default;
            int status = WriteApi.WriteMemory(table, layout, options is null ? null : &value, &buffer);
            byte[] bytes = buffer.Data is null ? [] : new ReadOnlySpan<byte>(buffer.Data, (int)buffer.Length).ToArray();
            WriteApi.FreeBuffer(&buffer);
            return (status, bytes, status == NativeStatus.Ok ? "" : LastError());
        }

        private static byte[] RoundTrip(string content, NativeLayout readLayout, NativeLayout writeLayout, NativeWriteOptionsRaw? options = null)
        {
            var table = ReadTable(Encoding.UTF8.GetBytes(content), readLayout);
            try
            {
                var (status, bytes, error) = WriteMemory(&table, writeLayout, options);
                Assert.True(status == NativeStatus.Ok, error);
                return bytes;
            }
            finally
            {
                NativeTable.Free(&table);
            }
        }

        [Fact]
        public void Write_EveryType_RoundTripsThroughRead()
        {
            var layout = EveryTypeLayout();
            var original = FromMemory(Encoding.UTF8.GetBytes(EveryType), layout);

            byte[] written = RoundTrip(EveryType, layout, layout);
            var reread = FromMemory(written, layout);

            Assert.Equal(original.RowCount, reread.RowCount);
            for (int i = 0; i < 7; i++)
            {
                Assert.Equal(original.Column(i), reread.Column(i));
            }
            Assert.Equal(2 * 56, written.Length);
        }

        [Fact]
        public void Write_ScaledZeroPadded_RoundTrips()
        {
            var layout = Build(new Col("preabe", 0, 13, TFloat64) { Scale = 2, Padding = '0' }, new Col("voltot", 13, 18, TDecimal) { Scale = 2, Padding = '0' });
            const string Content =
                "0000000123456" + "000000000000123456" + "\n" +
                "-000000012345" + "-00000000000000001" + "\n" +
                "0000000000000" + "000000000000000000" + "\n";

            Assert.Equal(Content, Encoding.UTF8.GetString(RoundTrip(Content, layout, layout)));
        }

        [Fact]
        public void Write_Null_FillsWithTheTrimCharacter()
        {
            var layout = Build(new Col("n", 0, 4, TInt64) { Nullable = true, TrimChar = '0', Padding = '0' }, new Col("s", 4, 3, TString) { Nullable = true });

            Assert.Equal("0012abc\n0000   \n", Encoding.UTF8.GetString(RoundTrip("0012abc\n0000   \n", layout, layout)));
        }

        [Fact]
        public void Write_StringTruncate_KeepsWholeCharacters()
        {
            var read = Build(new Col("s", 0, 7, TString));
            var write = Build(new Col("s", 0, 2, TString) { Overflow = 1 });

            Assert.Equal("A \n", Encoding.UTF8.GetString(RoundTrip("Açores\n", read, write)));
        }

        [Fact]
        public void Write_StringOverflowThrow_IsError()
        {
            var read = Build(new Col("s", 0, 6, TString));
            var write = Build(new Col("s", 0, 3, TString));
            var table = ReadTable("ABCDEF\n"u8.ToArray(), read);
            try
            {
                var (status, _, error) = WriteMemory(&table, write);

                Assert.Equal(NativeStatus.Error, status);
                Assert.Contains("exceeds the width 3", error, StringComparison.Ordinal);
            }
            finally
            {
                NativeTable.Free(&table);
            }
        }

        [Fact]
        public void Write_NumberOverflow_IsError()
        {
            var read = Build(new Col("n", 0, 5, TInt64));
            var write = Build(new Col("n", 0, 2, TInt64));
            var table = ReadTable("12345\n"u8.ToArray(), read);
            try
            {
                Assert.Equal(NativeStatus.Error, WriteMemory(&table, write).Status);
            }
            finally
            {
                NativeTable.Free(&table);
            }
        }

        [Fact]
        public void Write_TypeMismatch_IsInvalidArgument()
        {
            var read = Build(new Col("n", 0, 5, TInt64));
            var table = ReadTable("12345\n"u8.ToArray(), read);
            try
            {
                var (status, _, error) = WriteMemory(&table, Build(new Col("n", 0, 5, TString)));

                Assert.Equal(NativeStatus.InvalidArgument, status);
                Assert.Contains("'n'", error, StringComparison.Ordinal);
                Assert.Equal(NativeStatus.InvalidArgument, WriteMemory(&table, Build(new Col("a", 0, 2, TInt64), new Col("b", 2, 2, TInt64))).Status);
            }
            finally
            {
                NativeTable.Free(&table);
            }
        }

        public static TheoryData<int, long> Batches()
        {
            return new TheoryData<int, long> { { 1, 1 }, { 1, 3 }, { 4, 1 }, { 4, 3 }, { 4, 8192 }, { 0, 0 } };
        }

        [Theory]
        [MemberData(nameof(Batches))]
        public void Write_Parallel_IsByteIdenticalToSerial(int degree, long batchRows)
        {
            var layout = EveryTypeLayout();
            string content = string.Concat(Enumerable.Repeat(EveryType, 40));

            byte[] serial = RoundTrip(content, layout, layout, WriteOptions(degree: 1));
            byte[] parallel = RoundTrip(content, layout, layout, WriteOptions(degree, batchRows));

            Assert.Equal(serial, parallel);
        }

        [Fact]
        public void Write_File_EqualsMemory()
        {
            var layout = EveryTypeLayout();
            var table = ReadTable(Encoding.UTF8.GetBytes(EveryType), layout);
            string path = Path.GetTempFileName();
            try
            {
                var options = WriteOptions(degree: 4, batchRows: 1);
                Assert.Equal(NativeStatus.Ok, WriteApi.WriteFile(Encoding.UTF8.GetBytes(path), &table, layout, &options));

                Assert.Equal(WriteMemory(&table, layout).Bytes, File.ReadAllBytes(path));
            }
            finally
            {
                NativeTable.Free(&table);
                File.Delete(path);
            }
        }

        [Fact]
        public void Write_Latin1_EncodesText()
        {
            var layout = Build(new Col("s", 0, 6, TString));

            byte[] written = RoundTrip("AÇÃO  \n", layout, layout, WriteOptions(encoding: 28591));

            Assert.Equal(Encoding.Latin1.GetBytes("AÇÃO  \n"), written);
        }

        [Fact]
        public void Write_NewLineAndFiller_AreHonored()
        {
            var read = Build(new Col("a", 0, 2, TString), new Col("b", 2, 2, TString));
            var write = Build(LayoutOptions(lineLength: 8, filler: '.'), new Col("a", 0, 2, TString), new Col("b", 4, 2, TString));
            byte[] newline = "\r\n"u8.ToArray();
            fixed (byte* nl = newline)
            {
                var options = WriteOptions() with { NewLine = nl, NewLineLen = 2 };

                Assert.Equal("ab..cd..\r\n", Encoding.UTF8.GetString(RoundTrip("abcd\n", read, write, options)));
            }
        }

        [Fact]
        public void Write_TemporalWithoutFormat_IsIso()
        {
            var read = Build(new Col("d", 0, 8, TDate) { Format = "yyyyMMdd" });
            var write = Build(new Col("d", 0, 10, TDate));

            Assert.Equal("2023-10-24\n", Encoding.UTF8.GetString(RoundTrip("20231024\n", read, write)));
        }

        [Fact]
        public void Write_EmptyTable_IsEmpty()
        {
            var layout = Build(new Col("a", 0, 2, TString));

            Assert.Empty(RoundTrip("", layout, layout));
        }

        [Fact]
        public void Write_BadOptionsStructSize_IsInvalidArgument()
        {
            var layout = Build(new Col("a", 0, 2, TString));
            var table = ReadTable("ab\n"u8.ToArray(), layout);
            try
            {
                Assert.Equal(NativeStatus.InvalidArgument, WriteMemory(&table, layout, WriteOptions() with { StructSize = 4 }).Status);
            }
            finally
            {
                NativeTable.Free(&table);
            }
        }

        [Fact]
        public void BufferStruct_HasTheHeaderLayout()
        {
            Assert.Equal(16, Marshal.SizeOf<NativeBufferRaw>());
            Assert.Equal(32, Marshal.SizeOf<NativeWriteOptionsRaw>());
        }
    }
}
