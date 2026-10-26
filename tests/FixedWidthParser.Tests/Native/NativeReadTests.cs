using System.Text;
using FixedWidthParser.Native;
using FixedWidthParser.Native.Layouts;
using FixedWidthParser.Native.Reading;
using static FixedWidthParser.Tests.Native.NativeTestLayouts;
using static FixedWidthParser.Tests.Native.NativeTestReads;

namespace FixedWidthParser.Tests.Native
{
    public sealed unsafe class NativeReadTests
    {
        private static readonly DateOnly Epoch = new(1970, 1, 1);

        private static int Days(int year, int month, int day)
        {
            return new DateOnly(year, month, day).DayNumber - Epoch.DayNumber;
        }

        private static ReadOutcome[] Ok(string content, NativeLayout layout, NativeReadOptionsRaw? options = null, Encoding? encoding = null)
        {
            var outcomes = Both(content, layout, options, encoding);
            foreach (var outcome in outcomes)
            {
                Assert.True(outcome.Status == NativeStatus.Ok, $"status {outcome.Status}: {outcome.Error}");
            }
            return outcomes;
        }

        [Fact]
        public void Read_EveryType_ProducesColumns()
        {
            var layout = Build(
                new Col("name", 0, 6, TString),
                new Col("qty", 6, 5, TInt64),
                new Col("price", 11, 8, TFloat64),
                new Col("amount", 19, 8, TDecimal) { Scale = 2 },
                new Col("day", 27, 8, TDate) { Format = "yyyyMMdd" },
                new Col("time", 35, 6, TTime) { Format = "HHmmss" },
                new Col("stamp", 41, 14, TTimestamp) { Format = "yyyyMMddHHmmss" });
            const string Content =
                "Ann   " + "00042" + "  12.500" + "00012345" + "20231024" + "133005" + "20231024133000" + "\n" +
                "Bob   " + "   -7" + "    -0.5" + "-0000001" + "19700101" + "000000" + "19700101000000" + "\n";

            foreach (var outcome in Ok(Content, layout))
            {
                Assert.Equal(2, outcome.RowCount);
                Assert.Equal(["Ann", "Bob"], outcome.Column(0));
                Assert.Equal([42L, -7L], outcome.Column(1));
                Assert.Equal([12.5, -0.5], outcome.Column(2));
                Assert.Equal([12345L, -1L], outcome.Column(3));
                Assert.Equal([Days(2023, 10, 24), 0], outcome.Column(4));
                Assert.Equal([new TimeOnly(13, 30, 5).Ticks / 10, 0L], outcome.Column(5));
                Assert.Equal([(new DateTime(2023, 10, 24, 13, 30, 0, DateTimeKind.Utc) - DateTime.UnixEpoch).Ticks / 10, 0L], outcome.Column(6));
                Assert.Equal(2, outcome.Columns[3].Scale);
                Assert.Equal(2, outcome.LinesRead);
            }
        }

        [Fact]
        public void Read_ScaledFloat_DividesByPowerOfTen()
        {
            var layout = Build(new Col("preabe", 0, 13, TFloat64) { Scale = 2 });

            foreach (var outcome in Ok("0000000123456\n0000000000000\n", layout))
            {
                Assert.Equal([1234.56, 0.0], outcome.Column(0));
            }
        }

        [Fact]
        public void Read_ScaledNegative_KeepsSign()
        {
            var layout = Build(new Col("v", 0, 11, TFloat64) { Scale = 2 });

            foreach (var outcome in Ok("-0000012345\n", layout))
            {
                Assert.Equal([-123.45], outcome.Column(0));
            }
        }

        [Fact]
        public void Read_ScaledWithDecimalPoint_ParsesTheText()
        {
            var layout = Build(new Col("v", 0, 8, TFloat64) { Scale = 2 }, new Col("d", 8, 8, TDecimal) { Scale = 2 });

            foreach (var outcome in Ok("  123.45   67.8 \n", layout))
            {
                Assert.Equal([123.45], outcome.Column(0));
                Assert.Equal([6780L], outcome.Column(1));
            }
        }

        [Fact]
        public void Read_DecimalWithTooManyFractionalDigits_IsParseError()
        {
            var layout = Build(new Col("d", 0, 8, TDecimal) { Scale = 2 });

            Assert.All(Both("   1.234\n", layout), o => Assert.Equal(NativeStatus.ParseError, o.Status));
        }

        [Fact]
        public void Read_Decimal_KeepsUnscaled()
        {
            var layout = Build(new Col("voltot", 0, 18, TDecimal) { Scale = 2 });

            foreach (var outcome in Ok("000000000000123456\n", layout))
            {
                Assert.Equal([123456L], outcome.Column(0));
            }
        }

        [Fact]
        public void Read_Nullable_BlankIsNull()
        {
            var layout = Build(new Col("a", 0, 4, TInt64) { Nullable = true }, new Col("s", 4, 3, TString) { Nullable = true });

            foreach (var outcome in Ok("0001abc\n       \n0003xyz\n", layout))
            {
                Assert.Equal([1L, null, 3L], outcome.Column(0));
                Assert.Equal(["abc", null, "xyz"], outcome.Column(1));
            }
        }

        [Fact]
        public void Read_NoNulls_HasNoValidity()
        {
            var layout = Build(new Col("a", 0, 4, TInt64) { Nullable = true });

            foreach (var outcome in Ok("0001\n0002\n", layout))
            {
                Assert.True(outcome.Columns[0].Validity is null);
            }
        }

        [Fact]
        public void Read_NotNullableBlank_IsParseError()
        {
            var layout = Build(new Col("a", 0, 4, TInt64));

            foreach (var outcome in Both("0001\n    \n", layout))
            {
                Assert.Equal(NativeStatus.ParseError, outcome.Status);
                Assert.Contains("Line 2", outcome.Error, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Read_NotNullableBlankString_IsEmpty()
        {
            var layout = Build(new Col("s", 0, 4, TString), new Col("n", 4, 1, TInt64));

            foreach (var outcome in Ok("    1\n", layout))
            {
                Assert.Equal([""], outcome.Column(0));
            }
        }

        [Fact]
        public void Read_HeaderLines_AreNotParsed()
        {
            var layout = Build(new Col("n", 0, 3, TInt64));

            foreach (var outcome in Ok("HEADER\nXX\n001\n002\n", layout, Options(headerLines: 2)))
            {
                Assert.Equal([1L, 2L], outcome.Column(0));
                Assert.Equal(2, outcome.LinesSkipped);
            }
        }

        [Fact]
        public void Read_HeaderLinesBeyondTheFile_ReadNothing()
        {
            var layout = Build(new Col("n", 0, 3, TInt64));

            foreach (var outcome in Ok("HDR\n", layout, Options(headerLines: 5)))
            {
                Assert.Equal(0, outcome.RowCount);
                Assert.Equal(1, outcome.LinesSkipped);
            }
        }

        [Fact]
        public void Read_SkipInvalid_SkipsTrailerAndCounts()
        {
            var layout = Build(new Col("tipreg", 0, 2, TString), new Col("price", 2, 5, TFloat64) { Scale = 2 });
            const string Content = "00COTAHIST\n0100123\n0100456\n99TRAILER\n";

            foreach (var outcome in Ok(Content, layout, Options(skipInvalid: true)))
            {
                Assert.Equal(["01", "01"], outcome.Column(0));
                Assert.Equal([1.23, 4.56], outcome.Column(1));
                Assert.Equal(2, outcome.LinesSkipped);
                Assert.Equal(2, outcome.LinesRead);
            }
        }

        [Fact]
        public void Read_RejectedLineMidway_LeavesNoPartialValues()
        {
            var layout = Build(new Col("s", 0, 3, TString), new Col("n", 3, 2, TInt64), new Col("t", 5, 2, TString));

            foreach (var outcome in Ok("abc01xy\ndefXXzz\nghi03uv\n", layout, Options(skipInvalid: true)))
            {
                Assert.Equal(["abc", "ghi"], outcome.Column(0));
                Assert.Equal([1L, 3L], outcome.Column(1));
                Assert.Equal(["xy", "uv"], outcome.Column(2));
            }
        }

        [Fact]
        public void Read_NullableMixedSpacesAndTrimChar_IsNull()
        {
            var layout = Build(new Col("n", 0, 5, TInt64) { Nullable = true, TrimChar = '0' });

            foreach (var outcome in Ok(" 0 0 \n0 0 0\n00 12\n", layout))
            {
                Assert.Equal([null, null, 12L], outcome.Column(0));
            }
        }

        /// <summary>A valid row, one whose value overflows <see cref="decimal"/> once scaled, another valid row.</summary>
        internal const string DecimalOverflow = "              0.000000000000001\n79228162514264337593543950335.0\n              0.000000000000002\n";

        internal static NativeLayout DecimalOverflowLayout()
        {
            return Build(new Col("d", 0, 31, TDecimal) { Scale = 18 });
        }

        [Fact]
        public void Read_DecimalOverflowingTheScale_IsSkippedOrParseError()
        {
            var layout = DecimalOverflowLayout();

            foreach (var outcome in Ok(DecimalOverflow, layout, Options(skipInvalid: true)))
            {
                Assert.Equal([1000L, 2000L], outcome.Column(0));
                Assert.Equal(1, outcome.LinesSkipped);
            }
            foreach (var outcome in Both(DecimalOverflow, layout))
            {
                Assert.Equal(NativeStatus.ParseError, outcome.Status);
                Assert.Contains("column 'd'", outcome.Error, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Read_ParseError_ReportsLineAndColumn()
        {
            var layout = Build(new Col("name", 0, 3, TString), new Col("age", 3, 2, TInt64));

            foreach (var outcome in Both("Ann30\nBobXX\n", layout))
            {
                Assert.Equal(NativeStatus.ParseError, outcome.Status);
                Assert.Contains("column 'age'", outcome.Error, StringComparison.Ordinal);
                Assert.Contains("\"XX\"", outcome.Error, StringComparison.Ordinal);
                NativeApi.LastParseError(out long line, out int column);
                Assert.Equal(2, line);
                Assert.Equal(1, column);
            }
        }

        [Fact]
        public void Read_ShortLine_IsParseErrorWithColumnMinusOne()
        {
            var layout = Build(new Col("a", 0, 3, TString), new Col("b", 3, 4, TString));

            foreach (var outcome in Both("abc\n", layout))
            {
                Assert.Equal(NativeStatus.ParseError, outcome.Status);
                Assert.Contains("it is 3 bytes long and the layout needs 7", outcome.Error, StringComparison.Ordinal);
                NativeApi.LastParseError(out _, out int column);
                Assert.Equal(-1, column);
            }
        }

        [Fact]
        public void Read_Latin1Text_IsTranscodedToUtf8()
        {
            var layout = Build(new Col("name", 0, 6, TString), new Col("n", 6, 2, TInt64));
            var latin1 = Encoding.Latin1;

            foreach (var outcome in Ok("AÇÃO  12\n", layout, Options(encoding: 28591), latin1))
            {
                Assert.Equal(["AÇÃO"], outcome.Column(0));
                Assert.Equal([12L], outcome.Column(1));
            }
        }

        [Fact]
        public void Read_InvalidUtf8_IsReplacedLikeTheCore()
        {
            var layout = Build(new Col("name", 0, 6, TString), new Col("n", 6, 2, TInt64));
            byte[] latin1 = Encoding.Latin1.GetBytes("AÇÃO  01\n");

            var outcome = FromMemory(latin1, layout);

            Assert.Equal(NativeStatus.Ok, outcome.Status);
            Assert.Equal([Encoding.UTF8.GetString(latin1.AsSpan(0, 4))], outcome.Column(0));
        }

        [Fact]
        public void Read_SplitMultiByteCharacter_StaysValidUtf8()
        {
            var layout = Build(new Col("a", 0, 2, TString), new Col("b", 2, 3, TString));
            byte[] bytes = Encoding.UTF8.GetBytes("AÇBC\n");

            var outcome = FromMemory(bytes, layout);

            Assert.Equal(["A�", "�BC"], outcome.Column(0).Concat(outcome.Column(1)));
        }

        [Fact]
        public void Read_TimestampWithZoneOrOffset_IsUtc()
        {
            var layout = Build(new Col("t", 0, 25, TTimestamp) { Nullable = true });
            long midnight = (new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) - DateTime.UnixEpoch).Ticks / 10;

            foreach (var outcome in Ok("2024-01-01T00:00:00Z     \n2024-01-01T00:00:00-03:00\n2024-01-01T00:00:00      \n", layout))
            {
                Assert.Equal([midnight, midnight + (3L * 3600 * 1_000_000), midnight], outcome.Column(0));
            }
        }

        [Fact]
        public void Read_DecimalOver18Digits_IsParseError()
        {
            var layout = Build(new Col("d", 0, 19, TDecimal) { Scale = 2 });

            Assert.All(Both("1234567890123456789\n", layout), o => Assert.Equal(NativeStatus.ParseError, o.Status));
        }

        [Fact]
        public void Read_ScaledFloatBeyondInt64_KeepsTheScale()
        {
            var layout = Build(new Col("v", 0, 20, TFloat64) { Scale = 2 });

            foreach (var outcome in Ok("99999999999999999999\n", layout))
            {
                Assert.Equal([1e18], outcome.Column(0));
            }
        }

        [Fact]
        public void Read_EmptyFile_ReturnsZeroRows()
        {
            var layout = Build(new Col("a", 0, 3, TString));

            foreach (var outcome in Ok("", layout))
            {
                Assert.Equal(0, outcome.RowCount);
                Assert.Equal(1, outcome.Columns.Length);
            }
        }

        [Fact]
        public void Read_NullOptions_UsesDefaults()
        {
            var layout = Build(new Col("a", 0, 3, TString));

            foreach (var outcome in Ok("﻿abc\r\ndef", layout))
            {
                Assert.Equal(["abc", "def"], outcome.Column(0));
            }
        }

        [Fact]
        public void Read_BadOptionsStructSize_IsInvalidArgument()
        {
            var layout = Build(new Col("a", 0, 3, TString));

            var outcome = FromMemory("abc"u8.ToArray(), layout, Options() with { StructSize = 12 });

            Assert.Equal(NativeStatus.InvalidArgument, outcome.Status);
            Assert.Contains("fw_read_options struct_size is 12", outcome.Error, StringComparison.Ordinal);
        }

        [Fact]
        public void Read_MissingFile_IsError()
        {
            var layout = Build(new Col("a", 0, 3, TString));
            byte[] path = Encoding.UTF8.GetBytes(Path.Combine(Path.GetTempPath(), "fwp-missing-" + Guid.NewGuid()));

            unsafe
            {
                NativeTableRaw table = default;
                Assert.Equal(NativeStatus.Error, ReadApi.ReadFile(path, layout, null, &table, null));
            }
        }
    }
}
