using FixedWidthParser.Parsers;
using FixedWidthParser.Writers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    public class RoundTripTests
    {
        public static IEnumerable<object[]> PersonCases()
        {
            return [
            ["John Doe", 30, 60000.00],
            ["Alice", 25, 100.50],
            ["Bob", 0, 0.0],
            ["Max", -5, -1234.56],
            ["1234567890", 99999, 9999.99],
        ];
        }

        [Theory]
        [MemberData(nameof(PersonCases))]
        public void RoundTrip_Person_PreservesValues(string name, int age, double salary)
        {
            var writer = new FixedWidthWriter<PersonModel>();
            var parser = new FixedWidthParser<PersonModel>();
            var original = new PersonModel { Name = name, Age = age, Salary = salary };

            string line = WriteOne(writer, original);
            bool ok = parser.TryParse(line, Inv, null, out var parsed);

            Assert.True(ok);
            Assert.Equal(original.Name, parsed.Name);
            Assert.Equal(original.Age, parsed.Age);
            Assert.Equal(original.Salary, parsed.Salary, 2);
        }

        [Fact]
        public void RoundTrip_FieldModel_PreservesValues()
        {
            var writer = new FixedWidthWriter<ProductModel>();
            var parser = new FixedWidthParser<ProductModel>();
            var original = new ProductModel { Code = "ABCDE", Quantity = 1234 };

            string line = WriteOne(writer, original);
            bool ok = parser.TryParse(line, Inv, null, out var parsed);

            Assert.True(ok);
            Assert.Equal(original.Code, parsed.Code);
            Assert.Equal(original.Quantity, parsed.Quantity);
        }
    }
}
