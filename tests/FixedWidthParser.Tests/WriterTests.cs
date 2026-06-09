using FixedWidthParser.Writers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    public class WriterTests
    {
        [Fact]
        public void Write_ProducesFixedWidthLayout()
        {
            var writer = new FixedWidthWriter<PersonModel>();

            string line = WriteOne(writer, new PersonModel { Name = "John Doe", Age = 30, Salary = 60000 });

            Assert.Equal(25, line.Length);
            Assert.Equal("John Doe  ", line[..10]);
            Assert.Equal("30   ", line[10..15]);
            Assert.Equal("60000     ", line[15..25]);
        }

        [Fact]
        public void Write_StringLongerThanColumn_Truncates()
        {
            var writer = new FixedWidthWriter<PersonModel>();

            string line = WriteOne(writer, new PersonModel { Name = "VeryLongName", Age = 1, Salary = 0 });

            Assert.Equal("VeryLongNa", line[..10]);
        }

        [Fact]
        public void Write_EmptyString_PadsWithSpaces()
        {
            var writer = new FixedWidthWriter<PersonModel>();

            string line = WriteOne(writer, new PersonModel { Name = string.Empty, Age = 1, Salary = 0 });

            Assert.Equal("          ", line[..10]);
        }

        [Fact]
        public void Write_FieldBasedModel_Writes()
        {
            var writer = new FixedWidthWriter<ProductModel>();

            string line = WriteOne(writer, new ProductModel { Code = "AB", Quantity = 7 });

            Assert.Equal("AB   ", line[..5]);
            Assert.Equal("7   ", line[5..9]);
        }

        [Fact]
        public void WriteMany_SpanAndEnumerable_ProduceSameOutput()
        {
            var writer = new FixedWidthWriter<PersonModel>();
            var models = new[]
            {
                new PersonModel { Name = "Alice", Age = 25, Salary = 100 },
                new PersonModel { Name = "Bob",   Age = 40, Salary = 200 },
            };

            string spanOutput = WriteMany(writer, models.AsSpan());
            string enumerableOutput = WriteMany(writer, (IEnumerable<PersonModel>)models);

            Assert.Equal(enumerableOutput, spanOutput);
        }

        [Fact]
        public async Task WriteAsync_MatchesSyncOutput()
        {
            var writer = new FixedWidthWriter<PersonModel>();
            var model = new PersonModel { Name = "Alice", Age = 25, Salary = 100 };

#pragma warning disable S6966 // Awaitable method should be used
            string sync = WriteOne(writer, model);
#pragma warning restore S6966 // Awaitable method should be used
            string asyncResult = await WriteOneAsync(writer, model).ConfigureAwait(true);

            Assert.Equal(sync, asyncResult);
        }
    }
}
