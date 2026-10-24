using System.Text;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>A layout from <see cref="FixedWidthLayout.Build{TModel}"/> reads and writes exactly like the same layout declared with attributes.</summary>
    public sealed class FluentLayoutTests : IDisposable
    {
        private static readonly FixedWidthWriterOptions Lf = new() { NewLine = "\n" };

        private readonly string _path = Path.GetTempFileName();

        public void Dispose()
        {
            File.Delete(_path);
        }

        private static FixedWidthLayout<MutablePersonModel> Person()
        {
            return FixedWidthLayout.Build<MutablePersonModel>(b => b
                .Factory(() => new MutablePersonModel())
                .Column("Name", 0, 10, (in MutablePersonModel p) => p.Name, (ref MutablePersonModel p, string v) => p.Name = v)
                .Column<int>("Age", 10, 5, (in MutablePersonModel p) => p.Age, (ref MutablePersonModel p, int v) => p.Age = v)
                .Column<double>("Salary", 15, 10, (in MutablePersonModel p) => p.Salary, (ref MutablePersonModel p, double v) => p.Salary = v));
        }

        private static FixedWidthLayout<MutableOptionsModel> Options()
        {
            return FixedWidthLayout.Build<MutableOptionsModel>(b => b
                .Factory(() => new MutableOptionsModel())
                .Column<int>("ZeroPadded", 0, 5, (in MutableOptionsModel m) => m.ZeroPadded, (ref MutableOptionsModel m, int v) => m.ZeroPadded = v,
                    new FixedWidthColumnOptions { Alignment = Alignment.Right, Padding = '0' })
                .Column<double>("Fixed2", 5, 8, (in MutableOptionsModel m) => m.Fixed2, (ref MutableOptionsModel m, double v) => m.Fixed2 = v,
                    new FixedWidthColumnOptions { Format = "F2" })
                .Column("Truncated", 13, 4, (in MutableOptionsModel m) => m.Truncated, (ref MutableOptionsModel m, string v) => m.Truncated = v,
                    new FixedWidthColumnOptions { Alignment = Alignment.Right, Overflow = OverflowBehavior.Truncate })
                .Column<int>("Starred", 17, 5, (in MutableOptionsModel m) => m.Starred, (ref MutableOptionsModel m, int v) => m.Starred = v,
                    new FixedWidthColumnOptions { TrimChar = '*' })
                .NullableColumn<int>("NullableAge", 22, 5, (in MutableOptionsModel m) => m.NullableAge, (ref MutableOptionsModel m, int? v) => m.NullableAge = v)
                .NullableColumn<decimal>("NullableAmount", 27, 10, (in MutableOptionsModel m) => m.NullableAmount, (ref MutableOptionsModel m, decimal? v) => m.NullableAmount = v)
                .ConvertedColumn("Cents", 37, 8, (in MutableOptionsModel m) => m.Cents, (ref MutableOptionsModel m, CentsValue v) => m.Cents = v, new CentsConverter())
                .DecodedColumn<DateOnly>("Date", 45, 8, (in MutableOptionsModel m) => m.Date, (ref MutableOptionsModel m, DateOnly v) => m.Date = v,
                    new FixedWidthColumnOptions { Format = "yyyyMMdd" })
                .NullableDecodedColumn<DateOnly>("NullableDate", 53, 8, (in MutableOptionsModel m) => m.NullableDate, (ref MutableOptionsModel m, DateOnly? v) => m.NullableDate = v,
                    new FixedWidthColumnOptions { Format = "yyyyMMdd" })
                .NullableConvertedColumn("NullableCents", 61, 8, (in MutableOptionsModel m) => m.NullableCents, (ref MutableOptionsModel m, CentsValue? v) => m.NullableCents = v,
                    new CentsConverter()));
        }

        [Theory]
        [InlineData("John Doe  30   6000000.00")]
        [InlineData("Jane      28   55000.00  ")]
        [InlineData("John")]
        [InlineData("")]
        [InlineData("Ann       abc  10.5      ")]
        public void Fluent_TryParse_MatchesAttributes(string line)
        {
            bool okA = FixedWidth.TryParse(Bytes(line), FixedWidthLayout.FromAttributes<MutablePersonModel>(), Ctx, out var a);
            bool okF = FixedWidth.TryParse(Bytes(line), Person(), Ctx, out var f);

            Assert.Equal(okA, okF);
            if (okA)
            {
                Assert.Equal(a, f);
            }
        }

        [Fact]
        public void Fluent_WriteMany_MatchesAttributes()
        {
            MutablePersonModel[] people = [new() { Name = "John Doe", Age = 30, Salary = 6000000 }, new() { Name = "VeryLongName", Age = 1 }, new()];

            Assert.Equal(Write(people, FixedWidthLayout.FromAttributes<MutablePersonModel>()), Write(people, Person()));
        }

        [Theory]
        [InlineData(42, 3.14159, "ABCDEF", 7, 30, "12.5")]
        [InlineData(-3, -0.5, "X", 0, null, null)]
        public void Fluent_Options_MatchAttributes(int zero, double fixed2, string truncated, int starred, int? age, string? amount)
        {
            var model = new MutableOptionsModel
            {
                ZeroPadded = zero,
                Fixed2 = fixed2,
                Truncated = truncated,
                Starred = starred,
                NullableAge = age,
                NullableAmount = amount is null ? null : decimal.Parse(amount, Inv),
                Cents = new CentsValue(1234),
                Date = new DateOnly(2026, 10, 23),
                NullableDate = age is null ? null : new DateOnly(2027, 1, 2),
                NullableCents = age is null ? null : new CentsValue(99),
            };
            var attributes = FixedWidthLayout.FromAttributes<MutableOptionsModel>();

            string attributeLine = Write([model], attributes);
            Assert.Equal(attributeLine, Write([model], Options()));

            byte[] line = Bytes(attributeLine.TrimEnd('\n'));
            Assert.True(FixedWidth.TryParse(line, attributes, Ctx, out var a));
            Assert.True(FixedWidth.TryParse(line, Options(), Ctx, out var f));
            Assert.Equal(a, f);
        }

        [Fact]
        public void Fluent_StarTrim_ParsesLikeAttributes()
        {
            byte[] line = Bytes("00042" + "3.14    " + "ABCD" + "42***" + new string(' ', 15) + "1234    " + "20261023" + new string(' ', 16));

            Assert.True(FixedWidth.TryParse(line, FixedWidthLayout.FromAttributes<MutableOptionsModel>(), Ctx, out var a));
            Assert.True(FixedWidth.TryParse(line, Options(), Ctx, out var f));
            Assert.Equal(42, f.Starred);
            Assert.Equal(a, f);
        }

        [Fact]
        public void Fluent_ParseError_SameExceptionAsAttributes()
        {
            File.WriteAllText(_path, "John Doe  30   6000000.00\nJane      abc  55000.00  \n");

            var a = Assert.Throws<FixedWidthParseException>(() => FixedWidth.ReadFile(_path, FixedWidthLayout.FromAttributes<MutablePersonModel>()).ToList());
            var f = Assert.Throws<FixedWidthParseException>(() => FixedWidth.ReadFile(_path, Person()).ToList());

            Assert.Equal((a.LineNumber, a.ColumnName, a.ColumnStart, a.ColumnLength, a.RawValue), (f.LineNumber, f.ColumnName, f.ColumnStart, f.ColumnLength, f.RawValue));
            Assert.Equal(2, f.LineNumber);
            Assert.Equal("Age", f.ColumnName);
        }

        [Fact]
        public void Fluent_ShortLine_ReportsRequiredLength()
        {
            File.WriteAllText(_path, "John\n");

            var ex = Assert.Throws<FixedWidthParseException>(() => FixedWidth.ReadFile(_path, Person()).ToList());

            Assert.Contains("the layout needs 25", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Fluent_StructModel_SetterMutatesResult()
        {
            var layout = FixedWidthLayout.Build<MutablePersonStruct>(b => b
                .Column("Name", 0, 10, (in MutablePersonStruct p) => p.Name, (ref MutablePersonStruct p, string v) => p.Name = v)
                .Column<int>("Age", 10, 5, (in MutablePersonStruct p) => p.Age, (ref MutablePersonStruct p, int v) => p.Age = v)
                .Column<double>("Salary", 15, 10, (in MutablePersonStruct p) => p.Salary, (ref MutablePersonStruct p, double v) => p.Salary = v));

            Assert.True(FixedWidth.TryParse("John Doe  30   6000000.00"u8, layout, Ctx, out var p));

            Assert.Equal(new MutablePersonStruct { Name = "John Doe", Age = 30, Salary = 6000000 }, p);
        }

        [Fact]
        public void Fluent_RefStruct_TryParseAndTryFormat()
        {
            var layout = FixedWidthLayout.Build<RefPersonModel>(b => b
                .Column("Name", 0, 10, (in RefPersonModel p) => p.Name, (ref RefPersonModel p, string v) => p.Name = v)
                .Column<int>("Age", 10, 5, (in RefPersonModel p) => p.Age, (ref RefPersonModel p, int v) => p.Age = v)
                .Column<double>("Salary", 15, 10, (in RefPersonModel p) => p.Salary, (ref RefPersonModel p, double v) => p.Salary = v));
            Span<byte> buffer = stackalloc byte[25];

            Assert.True(FixedWidth.TryParse("John Doe  30   6000000   "u8, layout, Ctx, out var p));
            Assert.True(FixedWidth.TryFormat(in p, layout, buffer, Ctx, out int written));

            Assert.Equal("John Doe  30   6000000   ", Encoding.UTF8.GetString(buffer[..written]));
        }

        [Fact]
        public void Fluent_WriteOnlyColumn_ReadThrowsAtCall()
        {
            var layout = FixedWidthLayout.Build<MutablePersonModel>(b => b
                .Factory(() => new MutablePersonModel())
                .Column("Name", 0, 10, (in MutablePersonModel p) => p.Name, null));
            File.WriteAllText(_path, "John Doe  \n");

            var ex = Assert.Throws<InvalidOperationException>(() => FixedWidth.ReadFile(_path, layout));

            Assert.Contains("without a setter", ex.Message, StringComparison.Ordinal);
            Assert.Equal("John Doe  \n", Write([new MutablePersonModel { Name = "John Doe" }], layout));
        }

        [Fact]
        public void Fluent_ReadOnlyColumn_WriteThrows()
        {
            var layout = FixedWidthLayout.Build<MutablePersonModel>(b => b
                .Factory(() => new MutablePersonModel())
                .Column("Name", 0, 10, null, (ref MutablePersonModel p, string v) => p.Name = v));
            using var stream = new MemoryStream();

            var ex = Assert.Throws<InvalidOperationException>(() => FixedWidth.WriteMany(stream, [new MutablePersonModel()], layout));

            Assert.Contains("without a getter", ex.Message, StringComparison.Ordinal);
            Assert.True(FixedWidth.TryParse("John Doe  "u8, layout, Ctx, out var p));
            Assert.Equal("John Doe", p.Name);
        }

        [Fact]
        public void Fluent_Overlap_NamesColumns()
        {
            var attributes = Assert.Throws<InvalidOperationException>(() => FixedWidthLayout.FromAttributes<OverlapReadModel>());

            var fluent = Assert.Throws<InvalidOperationException>(() => FixedWidthLayout.Build<OverlapReadModel>(b => b
                .Column("Left", 0, 5, (in OverlapReadModel m) => m.Left, null)
                .Column("Right", 2, 5, (in OverlapReadModel m) => m.Right, null)));

            Assert.Equal(attributes.Message, fluent.Message);
        }

        [Fact]
        public void Fluent_NoColumns_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => FixedWidthLayout.Build<MutablePersonStruct>(_ => { }));

            Assert.Contains("has no columns", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Fluent_NegativeStart_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => FixedWidthLayout.Build<MutablePersonStruct>(b => b
                .Column("Name", -1, 10, (in MutablePersonStruct p) => p.Name, null)));

            Assert.Contains("negative Start", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Fluent_ZeroLength_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => FixedWidthLayout.Build<MutablePersonStruct>(b => b
                .Column("Name", 0, 0, (in MutablePersonStruct p) => p.Name, null)));

            Assert.Contains("invalid Length", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Fluent_NonAsciiPadding_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => FixedWidthLayout.Build<MutablePersonStruct>(b => b
                .Column("Name", 0, 10, (in MutablePersonStruct p) => p.Name, null, new FixedWidthColumnOptions { Padding = 'é' })));

            Assert.Contains("Padding", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Fluent_TruncateOnNonString_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => FixedWidthLayout.Build<MutablePersonStruct>(b => b
                .Column<int>("Age", 0, 5, (in MutablePersonStruct p) => p.Age, null, new FixedWidthColumnOptions { Overflow = OverflowBehavior.Truncate })));

            Assert.Contains("Truncate", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Fluent_ClassWithoutFactory_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => FixedWidthLayout.Build<MutablePersonModel>(b => b
                .Column("Name", 0, 10, (in MutablePersonModel p) => p.Name, (ref MutablePersonModel p, string v) => p.Name = v)));

            Assert.Contains("Factory", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Fluent_AggregateFileAsync_MatchesSequential()
        {
            var text = new StringBuilder();
            for (int i = 0; i < 100_000; i++)
            {
                text.Append(("N" + (i % 10)).PadRight(10)).Append((i % 1000).ToString(Inv).PadRight(5)).Append(i.ToString(Inv).PadRight(10)).Append('\n');
            }
            File.WriteAllText(_path, text.ToString());
            var layout = Person();

            var parallel = await FixedWidth.AggregateFileAsync<MutablePersonModel, Ages>(
                _path, layout, parallel: new FixedWidthParallelOptions { DegreeOfParallelism = 4 }, ct: TestContext.Current.CancellationToken);

            Assert.Equal(FixedWidth.ReadFile(_path, layout).Select(p => p.Age), parallel.Values);
            Assert.Equal(100_000, parallel.Values.Count);
        }

        public sealed class Ages : IRecordAccumulator<Ages, MutablePersonModel>
        {
            public List<int> Values { get; } = [];

            public void Add(in MutablePersonModel model)
            {
                Values.Add(model.Age);
            }

            public void Merge(Ages following)
            {
                Values.AddRange(following.Values);
            }
        }

        private static string Write<T>(T[] models, FixedWidthLayout<T> layout)
        {
            using var stream = new MemoryStream();
            FixedWidth.WriteMany(stream, models, layout, Lf);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
