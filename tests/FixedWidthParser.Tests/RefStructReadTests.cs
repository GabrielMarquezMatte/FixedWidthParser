using System.Text;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>Ref struct models read through every enumerator and aggregate, generated and through a layout alike.</summary>
    public sealed class RefStructReadTests : IDisposable
    {
        private const string Content = "Ann       30   1000.00   \nBob       41   2000.50   \r\nCid       52   3000.25   ";
        private const string BadContent = "Ann       30   1000.00   \nBob       XX   2000.50   \nCid       52   3000.25   ";

        private static readonly FixedWidthLayout<RefPersonModel> Layout = FixedWidthLayout.FromAttributes<RefPersonModel>();

        private readonly string _path = Path.GetTempFileName();

        public RefStructReadTests()
        {
            File.WriteAllText(_path, Content);
        }

        public void Dispose()
        {
            File.Delete(_path);
        }

        public sealed class GenAges : IRecordAccumulator<GenAges, GenRefPersonModel>
        {
            public List<int> Values { get; } = [];

            public void Add(in GenRefPersonModel model)
            {
                Values.Add(model.Age);
            }

            public void Merge(GenAges following)
            {
                Values.AddRange(following.Values);
            }
        }

        public sealed class Ages : IRecordAccumulator<Ages, RefPersonModel>
        {
            public List<int> Values { get; } = [];

            public void Add(in RefPersonModel model)
            {
                Values.Add(model.Age);
            }

            public void Merge(Ages following)
            {
                Values.AddRange(following.Values);
            }
        }

        private static MemoryStream Stream(string content)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }

        [Fact]
        public void Read_Generated_YieldsEveryRecord()
        {
            var names = new List<string>();
            foreach (var person in FixedWidth.Read<GenRefPersonModel>(Stream(Content)))
            {
                names.Add($"{person.Name}:{person.Age}:{person.Salary.ToString(Inv)}");
            }

            Assert.Equal(["Ann:30:1000", "Bob:41:2000.5", "Cid:52:3000.25"], names);
        }

        [Fact]
        public void ReadFile_Layout_YieldsEveryRecord()
        {
            var ages = new List<int>();
            foreach (var person in FixedWidth.ReadFile(_path, Layout))
            {
                ages.Add(person.Age);
            }

            Assert.Equal([30, 41, 52], ages);
        }

        [Fact]
        public void Read_ThroughIEnumerable_YieldsEveryRecord()
        {
            IEnumerable<GenRefPersonModel> records = FixedWidth.Read<GenRefPersonModel>(Stream(Content));
            var ages = new List<int>();
            using var enumerator = records.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ages.Add(enumerator.Current.Age);
            }

            Assert.Equal([30, 41, 52], ages);
        }

        [Fact]
        public void Read_CurrentReadTwice_ReturnsTheSameRecord()
        {
            using var enumerator = FixedWidth.Read<GenRefPersonModel>(Stream(Content)).GetEnumerator();
            Assert.True(enumerator.MoveNext());

            Assert.Equal(enumerator.Current.Name, enumerator.Current.Name);
            Assert.Equal(30, enumerator.Current.Age);
        }

        [Fact]
        public async Task ReadAsync_Generated_YieldsEveryRecord()
        {
            var ages = new List<int>();
            await foreach (var person in FixedWidth.ReadAsync<GenRefPersonModel>(Stream(Content)))
            {
                ages.Add(person.Age);
            }

            Assert.Equal([30, 41, 52], ages);
        }

        [Fact]
        public async Task ReadFileAsync_Layout_YieldsEveryRecord()
        {
            var ages = new List<int>();
            await foreach (var person in FixedWidth.ReadFileAsync(_path, Layout))
            {
                ages.Add(person.Age);
            }

            Assert.Equal([30, 41, 52], ages);
        }

        [Fact]
        public void Read_BadLineWithoutHandler_Throws()
        {
            var ex = Assert.Throws<FixedWidthParseException>(() =>
            {
                foreach (var person in FixedWidth.Read<GenRefPersonModel>(Stream(BadContent)))
                {
                    _ = person.Age;
                }
            });

            Assert.Equal(2, ex.LineNumber);
            Assert.Equal("Age", ex.ColumnName);
        }

        [Fact]
        public async Task ReadAsync_BadLineSkipped_YieldsTheRest()
        {
            var options = new FixedWidthReaderOptions { OnParseError = _ => true };
            var ages = new List<int>();
            await foreach (var person in FixedWidth.ReadAsync(Stream(BadContent), Layout, options: options))
            {
                ages.Add(person.Age);
            }

            Assert.Equal([30, 52], ages);
        }

        [Fact]
        public void Read_BadLineSkipped_YieldsTheRest()
        {
            var options = new FixedWidthReaderOptions { OnParseError = _ => true };
            var ages = new List<int>();
            foreach (var person in FixedWidth.Read<GenRefPersonModel>(Stream(BadContent), options: options))
            {
                ages.Add(person.Age);
            }

            Assert.Equal([30, 52], ages);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        public async Task AggregateFileAsync_FoldsEveryRecord(int degreeOfParallelism)
        {
            var options = new FixedWidthReaderOptions { DegreeOfParallelism = degreeOfParallelism, ChunkSize = 7 };

            var generated = await FixedWidth.AggregateFileAsync<GenRefPersonModel, GenAges>(_path, options);
            var layout = await FixedWidth.AggregateFileAsync<RefPersonModel, Ages>(_path, Layout, options);

            Assert.Equal([30, 41, 52], generated.Values);
            Assert.Equal([30, 41, 52], layout.Values);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        public async Task AggregateAsync_FoldsEveryRecord(int degreeOfParallelism)
        {
            var options = new FixedWidthReaderOptions { DegreeOfParallelism = degreeOfParallelism, ChunkSize = 7 };

            var generated = await FixedWidth.AggregateAsync<GenRefPersonModel, GenAges>(Stream(Content), options: options);
            var layout = await FixedWidth.AggregateAsync<RefPersonModel, Ages>(Stream(Content), Layout, options: options);

            Assert.Equal([30, 41, 52], generated.Values);
            Assert.Equal([30, 41, 52], layout.Values);
        }
    }
}
