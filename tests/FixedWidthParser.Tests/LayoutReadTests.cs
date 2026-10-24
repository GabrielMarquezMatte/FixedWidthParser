using System.Text;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>The <see cref="FixedWidth"/> read and aggregate overloads that take a layout.</summary>
    public sealed class LayoutReadTests : IDisposable
    {
        private const string People = "John Doe  30   6000000.00\nJane      28   55000.00  \nAnn       7    1.5       \n";

        private readonly string _path = Path.GetTempFileName();

        public void Dispose()
        {
            File.Delete(_path);
        }

        [Fact]
        public void ReadFile_WithLayout_YieldsSameRecordsAsReader()
        {
            File.WriteAllText(_path, People);

            var records = FixedWidth.ReadFile(_path, FixedWidthLayout.FromAttributes<PersonModel>())
                .Select(p => (p.Name, p.Age, p.Salary))
                .ToList();

            Assert.Equal([("John Doe", 30, 6000000.00), ("Jane", 28, 55000.00), ("Ann", 7, 1.5)], records);
        }

        [Fact]
        public async Task ReadAsync_WithLayout_ParseError_ReportsLineAndColumn()
        {
            using var stream = StreamOf("John Doe  30   6000000.00\nJane      abc  55000.00  \n");

            var ex = await Assert.ThrowsAsync<FixedWidthParseException>(async () =>
            {
                await foreach (var _ in FixedWidth.ReadAsync(stream, FixedWidthLayout.FromAttributes<PersonModel>()))
                {
                }
            });

            Assert.Equal(2, ex.LineNumber);
            Assert.Equal("Age", ex.ColumnName);
            Assert.Equal(10, ex.ColumnStart);
            Assert.Equal(5, ex.ColumnLength);
        }

        [Fact]
        public async Task AggregateFileAsync_WithLayout_MatchesSequentialFold()
        {
            File.WriteAllText(_path, NameAges(200_000));
            var layout = FixedWidthLayout.FromAttributes<NameAgeModel>();

            var parallel = await FixedWidth.AggregateFileAsync<NameAgeModel, ParallelAggregateTests.Ages>(
                _path, layout, parallel: new FixedWidthParallelOptions { DegreeOfParallelism = 4 }, ct: TestContext.Current.CancellationToken);

            Assert.Equal(SequentialAges(layout), parallel.Values);
        }

        [Fact]
        public async Task AggregateAsync_WithLayout_MatchesSequentialFold()
        {
            File.WriteAllText(_path, NameAges(200_000));
            var layout = FixedWidthLayout.FromAttributes<NameAgeModel>();
            using var stream = new MemoryStream(File.ReadAllBytes(_path));

            var parallel = await FixedWidth.AggregateAsync<NameAgeModel, ParallelAggregateTests.Ages>(
                stream, layout, parallel: new FixedWidthParallelOptions { DegreeOfParallelism = 4 }, ct: TestContext.Current.CancellationToken);

            Assert.Equal(SequentialAges(layout), parallel.Values);
        }

        private List<int> SequentialAges(FixedWidthLayout<NameAgeModel> layout)
        {
            var ages = FixedWidth.ReadFile(_path, layout).Select(m => m.Age).ToList();
            Assert.Equal(200_000, ages.Count);
            return ages;
        }

        private static string NameAges(int lines)
        {
            var text = new StringBuilder();
            for (int i = 1; i <= lines; i++)
            {
                text.Append("Name").Append(i % 10).Append(' ').Append((i % 1000).ToString("D3", Inv)).Append('\n');
            }
            return text.ToString();
        }
    }
}
