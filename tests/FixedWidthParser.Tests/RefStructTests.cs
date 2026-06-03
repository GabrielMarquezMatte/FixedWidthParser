using FixedWidthParser.Parsers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Confirms the parser's <c>allows ref struct</c> constraint is real: a ref struct model is
    /// built and parsed at runtime (Func/expression trees accept ref struct on .NET 10).
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
