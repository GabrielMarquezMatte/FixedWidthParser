using System.Text;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>The <see cref="FixedWidth"/> write overloads, for generated models and for layouts.</summary>
    public class LayoutWriteTests
    {
        private static readonly FixedWidthWriterOptions Lf = new() { NewLine = "\n" };

        private static readonly GenPersonModel[] GenPeople =
        [
            new() { Name = "John Doe", Age = 30, Salary = 6000000 },
            new() { Name = "Jane", Age = 28, Salary = 55000 },
            new() { Name = "Ann", Age = 7, Salary = 1.5 },
        ];

        private static readonly PersonModel[] People =
        [
            new() { Name = "John Doe", Age = 30, Salary = 6000000 },
            new() { Name = "Jane", Age = 28, Salary = 55000 },
            new() { Name = "Ann", Age = 7, Salary = 1.5 },
        ];

        [Fact]
        public void Generated_WriteMany_MatchesTryFormatPlusNewLine()
        {
            using var stream = new MemoryStream();

            FixedWidth.WriteMany(stream, GenPeople, Lf);

            Assert.Equal(string.Concat(GenPeople.Select(p => Format(p) + "\n")), Decode(stream));
        }

        [Fact]
        public void Generated_WriteMany_CrossesBatchBoundary()
        {
            int count = (3 * 64 * 1024 / 26) + 17;
            var models = Enumerable.Range(0, count).Select(i => new GenPersonModel { Name = "N" + i, Age = i % 1000, Salary = i }).ToList();
            using var stream = new MemoryStream();

            FixedWidth.WriteMany(stream, models, Lf);

            byte[] bytes = stream.ToArray();
            Assert.Equal(count * 26, bytes.Length);
            for (int i = 0; i < count; i++)
            {
                Assert.True(FixedWidth.TryParse<GenPersonModel>(bytes.AsSpan(i * 26, 25), Ctx, out var parsed));
                Assert.Equal(models[i], parsed);
            }
        }

        [Fact]
        public void Generated_Write_RecordLongerThanStackBuffer()
        {
            using var stream = new MemoryStream();

            FixedWidth.Write(stream, new GenLongNameModel { Name = "Wide" }, Lf);

            Assert.Equal("Wide".PadRight(1200) + "\n", Decode(stream));
        }

        [Fact]
        public void Generated_WriteMany_RecordLongerThanBatch()
        {
            using var stream = new MemoryStream();

            FixedWidth.WriteMany(stream, [new GenHugeNameModel { Name = "A" }, new GenHugeNameModel { Name = "B" }], Lf);

            Assert.Equal("A".PadRight(70_000) + "\n" + "B".PadRight(70_000) + "\n", Decode(stream));
        }

        [Fact]
        public async Task LongRecords_AsyncTwinsMatchSync()
        {
            GenHugeNameModel[] huge = [new() { Name = "A" }, new() { Name = "B" }];
            var hugeLayout = FixedWidthLayout.Build<GenHugeNameModel>(b => b.Column("Name", 0, 70_000, (in GenHugeNameModel m) => m.Name, null));
            var ct = TestContext.Current.CancellationToken;
            using var genSync = new MemoryStream();
            await using var genAsync = new MemoryStream();
            using var layoutSync = new MemoryStream();
            await using var layoutAsync = new MemoryStream();
            using var oneSync = new MemoryStream();
            await using var oneAsync = new MemoryStream();

            FixedWidth.WriteMany(genSync, huge, Lf);
            await FixedWidth.WriteManyAsync(genAsync, huge, Lf, ct);
            FixedWidth.WriteMany(layoutSync, huge, hugeLayout, Lf);
            await FixedWidth.WriteManyAsync(layoutAsync, huge, hugeLayout, Lf, ct);
            FixedWidth.Write(oneSync, new GenLongNameModel { Name = "Wide" }, Lf);
            await FixedWidth.WriteAsync(oneAsync, new GenLongNameModel { Name = "Wide" }, Lf, ct);

            string expected = "A".PadRight(70_000) + "\n" + "B".PadRight(70_000) + "\n";
            Assert.Equal(expected, Decode(genSync));
            Assert.Equal(expected, Decode(genAsync));
            Assert.Equal(expected, Decode(layoutSync));
            Assert.Equal(expected, Decode(layoutAsync));
            Assert.Equal(Decode(oneSync), Decode(oneAsync));
        }

        [Fact]
        public void Layout_WriteMany_MatchesWriter()
        {
            using var stream = new MemoryStream();

            FixedWidth.WriteMany(stream, People, FixedWidthLayout.FromAttributes<PersonModel>(), Lf);

            Assert.Equal(WriteMany(FixedWidthLayout.FromAttributes<PersonModel>(), (IEnumerable<PersonModel>)People, Lf), Decode(stream));
        }

        [Fact]
        public async Task WriteManyAsync_MatchesWriteMany()
        {
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();
            using var sync = new MemoryStream();
            await using var async = new MemoryStream();
            using var genSync = new MemoryStream();
            await using var genAsync = new MemoryStream();

            FixedWidth.WriteMany(sync, People, layout, Lf);
            await FixedWidth.WriteManyAsync(async, People, layout, Lf, TestContext.Current.CancellationToken);
            FixedWidth.WriteMany(genSync, GenPeople, Lf);
            await FixedWidth.WriteManyAsync(genAsync, GenPeople, Lf, TestContext.Current.CancellationToken);

            Assert.Equal(Decode(sync), Decode(async));
            Assert.Equal(Decode(genSync), Decode(genAsync));
            Assert.Equal(Decode(sync), Decode(genSync));
        }

        [Fact]
        public async Task WriteAsync_MatchesWrite()
        {
            using var sync = new MemoryStream();
            await using var async = new MemoryStream();

            FixedWidth.Write(sync, GenPeople[0], Lf);
            await FixedWidth.WriteAsync(async, GenPeople[0], Lf, TestContext.Current.CancellationToken);

            Assert.Equal(Format(GenPeople[0]) + "\n", Decode(sync));
            Assert.Equal(Decode(sync), Decode(async));
        }

        [Fact]
        public void Write_NullNewLine_ThrowsArgumentException()
        {
            using var stream = new MemoryStream();

            Assert.Throws<ArgumentException>(() => FixedWidth.Write(stream, GenPeople[0], new FixedWidthWriterOptions { NewLine = null! }));
        }

        private static string Decode(MemoryStream stream)
        {
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
