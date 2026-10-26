using System.Text;
using CsCheck;
using FixedWidthParser.Native;
using FixedWidthParser.Native.Layouts;
using static FixedWidthParser.Tests.Native.NativeTestLayouts;
using static FixedWidthParser.Tests.Native.NativeTestReads;

namespace FixedWidthParser.Tests.Native
{
    /// <summary>
    /// A partitioned read parses into slices of the final columns sized from the first line's length
    /// (<c>TableArena</c>), then closes the gaps in place or copies a column out when a slice overflows. Random files
    /// drive every path through it: invalid, empty and short lines, trailing bytes and CRLF that change the line
    /// length, a byte order mark and header lines, multi-byte text, and Latin-1 text whose UTF-8 is longer than the
    /// column. Whatever the chunk size and thread count, the result must be the sequential read's.
    /// </summary>
    public sealed class NativeParallelPropertyTests
    {
        private const int Latin1 = 28591;

        private sealed record Case(string[] Lines, int HeaderLines, bool Bom, bool FinalNewline, bool Latin, bool SkipInvalid, int ChunkSize, int Degree)
        {
            public byte[] Bytes()
            {
                var text = new StringBuilder();
                for (int i = 0; i < HeaderLines; i++)
                {
                    text.Append("HEADER ").Append(i).Append('\n');
                }
                text.AppendJoin("", Lines);
                if (!FinalNewline && text.Length > 0 && text[^1] == '\n')
                {
                    text.Length--;
                    if (text.Length > 0 && text[^1] == '\r')
                    {
                        text.Length--;
                    }
                }
                byte[] body = (Latin ? Encoding.Latin1 : Encoding.UTF8).GetBytes(text.ToString());
                return Bom && !Latin ? [0xEF, 0xBB, 0xBF, .. body] : body;
            }
        }

        // code[0,4) text, qty[4,8) nullable integer, price[8,14) two implied decimals.
        private static NativeLayout Layout()
        {
            return Build(
                new Col("code", 0, 4, TString),
                new Col("qty", 4, 4, TInt64) { Nullable = true },
                new Col("price", 8, 6, TFloat64) { Scale = 2 });
        }

        private static Gen<string> GenLine(bool latin)
        {
            // Four bytes in either encoding; as UTF-8, the Latin-1 one is eight.
            var code = Gen.OneOf(Gen.Char["ABCXYZ019"].Array[4].Select(c => new string(c)), Gen.Const(latin ? "ÉÉÉÉ" : "ÉÉ"));
            var qty = Gen.OneOf(Gen.Int[0, 9999].Select(q => q.ToString("D4", System.Globalization.CultureInfo.InvariantCulture)), Gen.Const("    "));
            var price = Gen.Int[0, 999_999].Select(p => p.ToString("D6", System.Globalization.CultureInfo.InvariantCulture));
            var tail = Gen.Char[" #"].Array[0, 3].Select(c => new string(c));
            var newline = Gen.OneOf(Gen.Const("\n"), Gen.Const("\r\n"));
            var valid = Gen.Select(code, qty, price, tail, newline, (c, q, p, t, n) => c + q + p + t + n);
            return Gen.Frequency(
                (12, valid),
                (1, newline.Select(n => "AB01XXXX123456" + n)),
                (1, newline.Select(n => "AB" + n)),
                (1, newline));
        }

        private static readonly Gen<Case> GenCase =
            from latin in Gen.Bool
            from lines in GenLine(latin).Array[0, 60]
            from header in Gen.Int[0, 2]
            from bom in Gen.Bool
            from finalNewline in Gen.Bool
            from skipInvalid in Gen.Bool
            from chunk in Gen.Int[1, 64]
            from degree in Gen.Int[2, 8]
            select new Case(lines, header, bom, finalNewline, latin, skipInvalid, chunk, degree);

        [Fact]
        public void ParallelRead_IsTheSequentialRead()
        {
            var layout = Layout();
            GenCase.Sample(c =>
            {
                byte[] bytes = c.Bytes();
                int encoding = c.Latin ? Latin1 : 0;

                var sequential = FromMemory(bytes, layout, Options(c.HeaderLines, c.SkipInvalid, degreeOfParallelism: 1, encoding: encoding));
                NativeApi.LastParseError(out long sequentialLine, out int sequentialColumn);
                var parallel = FromMemory(bytes, layout, Options(c.HeaderLines, c.SkipInvalid, degreeOfParallelism: c.Degree, encoding: encoding), c.ChunkSize);
                NativeApi.LastParseError(out long parallelLine, out int parallelColumn);

                Assert.Equal(sequential.Status, parallel.Status);
                if (sequential.Status != NativeStatus.Ok)
                {
                    Assert.Equal((sequentialLine, sequentialColumn), (parallelLine, parallelColumn));
                    return;
                }
                Assert.Equal(sequential.RowCount, parallel.RowCount);
                Assert.Equal(sequential.LinesSkipped, parallel.LinesSkipped);
                for (int column = 0; column < 3; column++)
                {
                    Assert.Equal(sequential.Column(column), parallel.Column(column));
                }
            }, iter: 2000);
        }
    }
}
