using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    public class WriterTests
    {
        [Fact]
        public void Write_ProducesFixedWidthLayout()
        {
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();

            string line = WriteOne(layout, new PersonModel { Name = "John Doe", Age = 30, Salary = 60000 });

            Assert.Equal(25, line.Length);
            Assert.Equal("John Doe  ", line[..10]);
            Assert.Equal("30   ", line[10..15]);
            Assert.Equal("60000     ", line[15..25]);
        }

        [Fact]
        public void Write_StringLongerThanColumn_Truncates()
        {
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();

            string line = WriteOne(layout, new PersonModel { Name = "VeryLongName", Age = 1, Salary = 0 });

            Assert.Equal("VeryLongNa", line[..10]);
        }

        [Fact]
        public void Write_EmptyString_PadsWithSpaces()
        {
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();

            string line = WriteOne(layout, new PersonModel { Name = string.Empty, Age = 1, Salary = 0 });

            Assert.Equal("          ", line[..10]);
        }

        [Fact]
        public void Write_FieldBasedModel_Writes()
        {
            var layout = FixedWidthLayout.FromAttributes<ProductModel>();

            string line = WriteOne(layout, new ProductModel { Code = "AB", Quantity = 7 });

            Assert.Equal("AB   ", line[..5]);
            Assert.Equal("7   ", line[5..9]);
        }

        [Fact]
        public void WriteMany_SpanAndEnumerable_ProduceSameOutput()
        {
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();
            var models = new[]
            {
                new PersonModel { Name = "Alice", Age = 25, Salary = 100 },
                new PersonModel { Name = "Bob",   Age = 40, Salary = 200 },
            };

            string spanOutput = WriteMany(layout, models.AsSpan());
            string enumerableOutput = WriteMany(layout, (IEnumerable<PersonModel>)models);

            Assert.Equal(enumerableOutput, spanOutput);
        }

        [Fact]
        public async Task WriteAsync_MatchesSyncOutput()
        {
            var layout = FixedWidthLayout.FromAttributes<PersonModel>();
            var model = new PersonModel { Name = "Alice", Age = 25, Salary = 100 };

#pragma warning disable S6966 // Awaitable method should be used
            string sync = WriteOne(layout, model);
#pragma warning restore S6966 // Awaitable method should be used
            string asyncResult = await WriteOneAsync(layout, model).ConfigureAwait(true);

            Assert.Equal(sync, asyncResult);
        }

        // ----------------------- Lines longer than 1024 (ArrayPool path) -----------------------

        [Fact]
        public void Write_LineLongerThan1024_UsesArrayPoolPath()
        {
            var layout = FixedWidthLayout.FromAttributes<WideLineModel>();

            string line = WriteOne(layout, new WideLineModel { Name = "Wide", Number = 42 });

            Assert.Equal(WideLineModel.LineLength, line.Length);
            Assert.Equal("Wide", line[..1200].TrimEnd());
            Assert.Equal("42", line[1200..1208].TrimEnd());
        }

        [Fact]
        public void WriteMany_Span_LineLongerThan1024_UsesArrayPoolPath()
        {
            var layout = FixedWidthLayout.FromAttributes<WideLineModel>();
            var models = new[]
            {
                new WideLineModel { Name = "Alice", Number = 1 },
                new WideLineModel { Name = "Bob",   Number = 2 },
            };

            string output = WriteMany(layout, models.AsSpan());

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                              .Select(l => l.TrimEnd('\r'))
                              .ToArray();
            Assert.Equal(2, lines.Length);
            Assert.All(lines, l => Assert.Equal(WideLineModel.LineLength, l.Length));
            Assert.Equal("Alice", lines[0][..1200].TrimEnd());
            Assert.Equal("1", lines[0][1200..1208].TrimEnd());
            Assert.Equal("Bob", lines[1][..1200].TrimEnd());
            Assert.Equal("2", lines[1][1200..1208].TrimEnd());
        }

        [Fact]
        public void WriteMany_SpanAndEnumerable_LineLongerThan1024_ProduceSameOutput()
        {
            var layout = FixedWidthLayout.FromAttributes<WideLineModel>();
            var models = new[]
            {
                new WideLineModel { Name = "Alice", Number = 1 },
                new WideLineModel { Name = "Bob",   Number = 2 },
            };

            string spanOutput = WriteMany(layout, models.AsSpan());
            string enumerableOutput = WriteMany(layout, (IEnumerable<WideLineModel>)models);

            Assert.Equal(enumerableOutput, spanOutput);
        }
    }
}
