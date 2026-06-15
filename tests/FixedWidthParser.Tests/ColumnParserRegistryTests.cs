using FixedWidthParser.Attributes;
using FixedWidthParser.Parsers;
using FixedWidthParser.Processors;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>A value type with no ISpanParsable, so it is parseable only via a registered parser.</summary>
    public readonly record struct Tag(string Value);

    /// <summary>A model whose only column is the custom <see cref="Tag"/> type. Top-level + public so the
    /// parser's compiled-expression accessors can reach it.</summary>
    public readonly record struct TagModel
    {
        [FixedColumn(0, 5)] public Tag Label { get; init; }
    }

    /// <summary>
    /// Covers the char-side <see cref="ColumnParserRegistry"/> extensibility surface, focusing on
    /// <see cref="ColumnParserRegistry.Unregister{TValue}"/>: it returns <see langword="true"/> when a
    /// parser was registered and removed, and <see langword="false"/> otherwise. A custom test-only type
    /// is used so the process-wide registry is left clean (each test registers then unregisters).
    /// </summary>
    public class ColumnParserRegistryTests
    {
        private static ColumnValueParser<Tag> TagParser()
        {
            return static (ReadOnlySpan<char> span, IFormatProvider? _, out Tag value) =>
            {
                value = new Tag(span.TrimEnd(' ').ToString());
                return true;
            };
        }

        [Fact]
        public void Unregister_AfterRegister_ReturnsTrue()
        {
            ColumnParserRegistry.Register(TagParser());

            bool removed = ColumnParserRegistry.Unregister<Tag>();

            Assert.True(removed);
        }

        [Fact]
        public void Unregister_WhenNeverRegistered_ReturnsFalse()
        {
            // A type the registry has never seen (and that isn't pre-registered like double/float).
            bool removed = ColumnParserRegistry.Unregister<Tag>();

            Assert.False(removed);
        }

        [Fact]
        public void Unregister_Twice_SecondCallReturnsFalse()
        {
            ColumnParserRegistry.Register(TagParser());

            Assert.True(ColumnParserRegistry.Unregister<Tag>());
            Assert.False(ColumnParserRegistry.Unregister<Tag>());
        }

        [Fact]
        public void Register_ThenUnregister_RoundTrips()
        {
            // Register before the first use of the parser for this model (it caches column parsers in a
            // static constructor), parse through the custom parser, then unregister to clean up.
            ColumnParserRegistry.Register(TagParser());
            try
            {
                var parser = new FixedWidthParser<TagModel>();

                bool ok = parser.TryParse("AB   ", Inv, null, out var model);

                Assert.True(ok);
                Assert.Equal(new Tag("AB"), model.Label);
            }
            finally
            {
                Assert.True(ColumnParserRegistry.Unregister<Tag>());
            }
        }
    }
}
