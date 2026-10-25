using System.Text;
using FixedWidthParser.Native;
using FixedWidthParser.Native.Layouts;
using static FixedWidthParser.Tests.Native.NativeTestLayouts;
using static FixedWidthParser.Tests.Native.NativeTestReads;

namespace FixedWidthParser.Tests.Native
{
    public sealed class NativeParallelReadTests
    {
        private static NativeLayout Layout()
        {
            return Build(
                new Col("code", 0, 4, TString),
                new Col("qty", 4, 4, TInt64) { Nullable = true },
                new Col("price", 8, 6, TFloat64) { Scale = 2 });
        }

        // A BOM, CRLF and LF breaks, empty lines, invalid lines, nulls and no final line break.
        private static byte[] Content(bool withBom)
        {
            var text = new StringBuilder(withBom ? "﻿" : "");
            text.Append("HEADER ONE\r\nHEADER TWO\n");
            for (int i = 1; i <= 200; i++)
            {
                string qty = i % 7 == 0 ? "    " : (i % 10000).ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
                string price = i % 13 == 0 ? "XXXXXX" : (i * 3).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
                text.Append(i % 3 == 0 ? "CD" : "AB").Append(System.Globalization.CultureInfo.InvariantCulture, $"{i % 100:D2}").Append(qty).Append(price);
                text.Append(i % 2 == 0 ? "\r\n" : "\n");
                if (i % 17 == 0)
                {
                    text.Append('\n');
                }
            }
            text.Append("LAST0001000123");
            return Encoding.UTF8.GetBytes(text.ToString());
        }

        public static TheoryData<int, int, bool> Partitions()
        {
            var data = new TheoryData<int, int, bool>();
            foreach (int chunkSize in new[] { 1, 7, 64, 4096 })
            {
                foreach (int degree in new[] { 1, 4 })
                {
                    data.Add(chunkSize, degree, false);
                    data.Add(chunkSize, degree, true);
                }
            }
            return data;
        }

        [Theory]
        [MemberData(nameof(Partitions))]
        public void ParallelRead_EqualsSequential(int chunkSize, int degree, bool withBom)
        {
            var layout = Layout();
            byte[] content = Content(withBom);

            var sequential = FromMemory(content, layout, Options(headerLines: 2, skipInvalid: true, degreeOfParallelism: 1));
            var parallel = FromMemory(content, layout, Options(headerLines: 2, skipInvalid: true, degreeOfParallelism: degree), chunkSize);

            Assert.Equal(NativeStatus.Ok, sequential.Status);
            Assert.True(parallel.Status == NativeStatus.Ok, parallel.Error);
            Assert.Equal(sequential.RowCount, parallel.RowCount);
            for (int column = 0; column < 3; column++)
            {
                Assert.Equal(sequential.Column(column), parallel.Column(column));
            }
            Assert.Equal(sequential.LinesRead, parallel.LinesRead);
            Assert.Equal(sequential.LinesSkipped, parallel.LinesSkipped);
            Assert.Equal(200 - (200 / 13) + 1, parallel.RowCount);
        }

        // Lines of one length with invalid ones among them leave gaps in the shared columns that publishing closes in
        // place; a longer first line makes every partition outgrow the room it was lent.
        [Theory]
        [InlineData(1, false)]
        [InlineData(7, false)]
        [InlineData(64, false)]
        [InlineData(4096, false)]
        [InlineData(7, true)]
        [InlineData(64, true)]
        public void ParallelRead_UniformLines_EqualsSequential(int chunkSize, bool longFirstLine)
        {
            var layout = Layout();
            var text = new StringBuilder(longFirstLine ? "AB000001000003 trailing\n" : "");
            for (int i = 1; i <= 300; i++)
            {
                string price = i % 11 == 0 ? "XXXXXX" : (i * 3).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
                text.Append(System.Globalization.CultureInfo.InvariantCulture, $"AB{i % 100:D2}{i % 10000:D4}{price}\n");
            }
            byte[] content = Encoding.UTF8.GetBytes(text.ToString());

            var sequential = FromMemory(content, layout, Options(skipInvalid: true, degreeOfParallelism: 1));
            var parallel = FromMemory(content, layout, Options(skipInvalid: true, degreeOfParallelism: 4), chunkSize);

            Assert.True(parallel.Status == NativeStatus.Ok, parallel.Error);
            Assert.Equal(300 - (300 / 11) + (longFirstLine ? 1 : 0), parallel.RowCount);
            Assert.Equal(sequential.RowCount, parallel.RowCount);
            for (int column = 0; column < 3; column++)
            {
                Assert.Equal(sequential.Column(column), parallel.Column(column));
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(64)]
        public void ParallelRead_ErrorLineNumber_EqualsSequential(int chunkSize)
        {
            var layout = Layout();
            byte[] content = Content(withBom: true);

            var sequential = FromMemory(content, layout, Options(headerLines: 2, degreeOfParallelism: 1));
            NativeApi.LastParseError(out long sequentialLine, out int sequentialColumn);
            var parallel = FromMemory(content, layout, Options(headerLines: 2, degreeOfParallelism: 4), chunkSize);
            NativeApi.LastParseError(out long parallelLine, out int parallelColumn);

            Assert.Equal(NativeStatus.ParseError, sequential.Status);
            Assert.Equal(NativeStatus.ParseError, parallel.Status);
            Assert.Equal(sequentialLine, parallelLine);
            Assert.Equal(sequentialColumn, parallelColumn);
            Assert.Equal(2, parallelColumn);
        }
    }
}
