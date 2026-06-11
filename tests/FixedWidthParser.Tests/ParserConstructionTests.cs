using FixedWidthParser.Parsers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Build-time paths of the runtime parsers. Both <see cref="FixedWidthParser{TModel}"/>
    /// and <see cref="Utf8FixedWidthParser{TModel}"/> build their column plan in a static constructor
    /// and capture any error, rethrowing it from the public constructor via
    /// <see cref="System.Runtime.ExceptionServices.ExceptionDispatchInfo"/>. These cover the
    /// struct-without-declared-constructor branch of <c>BuildModelFactory</c> (default
    /// initialization) and the error capture/rethrow (the layout-validation branches are covered by
    /// <see cref="ColumnValidationTests"/> for the char parser).
    /// </summary>
    public class ParserConstructionTests
    {
        // ----------------------------- char parser -----------------------------

        [Fact]
        public void Parser_StructWithoutDeclaredConstructor_Parses()
        {
            var parser = new FixedWidthParser<NoParameterlessCtorModel>();

            Assert.True(parser.TryParse("ABC", null, null, out var model));
            Assert.Equal("ABC", model.Code);
        }

        [Fact]
        public void Parser_BuildError_IsRethrownOnEveryConstruction()
        {
            // The error is captured once in the static constructor and rethrown on each construction.
            Assert.Throws<InvalidOperationException>(() => new FixedWidthParser<OverlapReadModel>());
            Assert.Throws<InvalidOperationException>(() => new FixedWidthParser<OverlapReadModel>());
        }

        // ----------------------------- UTF-8 parser -----------------------------

        [Fact]
        public void Utf8Parser_StructWithoutDeclaredConstructor_Parses()
        {
            var parser = new Utf8FixedWidthParser<NoParameterlessCtorModel>();

            Assert.True(parser.TryParse("ABC"u8, null, null, out var model));
            Assert.Equal("ABC", model.Code);
        }

        [Fact]
        public void Utf8Parser_BuildError_IsRethrownOnEveryConstruction()
        {
            Assert.Throws<InvalidOperationException>(() => new Utf8FixedWidthParser<OverlapReadModel>());
            Assert.Throws<InvalidOperationException>(() => new Utf8FixedWidthParser<OverlapReadModel>());
        }

        [Fact]
        public void Utf8Parser_OverlappingColumns_ThrowsOnConstruction()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new Utf8FixedWidthParser<OverlapReadModel>());

            Assert.Contains("Overlapping", ex.Message, StringComparison.Ordinal);
        }
    }
}
