using FixedWidthParser.Parsers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Confirma que a constraint <c>allows ref struct</c> do parser é real: um modelo ref struct
    /// é construído e parseado em runtime (Func/expression trees aceitam ref struct no .NET 10).
    /// </summary>
    public class RefStructTests
    {
        [Fact]
        public void Parser_SupportsRefStructModel()
        {
            var parser = new FixedWidthParser<RefPersonModel>();

            bool ok = parser.TryParse("John Doe  30   60000.00  ", Inv, null, out var model);

            Assert.True(ok);
            Assert.Equal("John Doe", model.Name);
            Assert.Equal(30, model.Age);
            Assert.Equal(60000.00, model.Salary, 2);
        }
    }
}
