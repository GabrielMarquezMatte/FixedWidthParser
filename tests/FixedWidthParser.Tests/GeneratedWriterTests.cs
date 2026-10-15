using System.Globalization;
using FixedWidthParser.Writers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// The source-generated <c>TryFormat</c> (via <see cref="FixedWidth.TryFormat{TModel}"/>): parity
    /// with <see cref="FixedWidthWriter{TModel}"/>, round-tripping through the generated <c>TryParse</c>,
    /// formatting options, nullable columns, converters, culture, and a destination that is too small.
    /// </summary>
    public class GeneratedWriterTests
    {
        private static string WriteGenerated<TModel>(in TModel model, IFormatProvider? formatProvider = null)
            where TModel : IFixedWidthModel<TModel>
        {
            return Format(in model, CtxWith(formatProvider));
        }

        [Theory]
        [InlineData("John Doe", 30, 60000.00)]
        [InlineData("Alice", 25, 100.50)]
        [InlineData("Max", -5, -1234.56)]
        public void Person_MatchesReflection(string name, int age, double salary)
        {
            var writer = new FixedWidthWriter<PersonModel>();

            Assert.Equal(
                WriteOne(writer, new PersonModel { Name = name, Age = age, Salary = salary }),
                WriteGenerated(new GenPersonModel { Name = name, Age = age, Salary = salary }));
        }

        [Fact]
        public void Person_RoundTripsThroughGeneratedParse()
        {
            var original = new GenPersonModel { Name = "Bob", Age = 42, Salary = 999.5 };

            string line = WriteGenerated(original);

            Assert.True(FixedWidth.TryParse<GenPersonModel>(Bytes(line), Ctx, out var parsed));
            Assert.Equal(original.Name, parsed.Name);
            Assert.Equal(original.Age, parsed.Age);
            Assert.Equal(original.Salary, parsed.Salary, 2);
        }

        [Fact]
        public void FieldModel_MatchesReflection()
        {
            Assert.Equal(
                WriteOne(new FixedWidthWriter<ProductModel>(), new ProductModel { Code = "ABCDE", Quantity = 1234 }),
                WriteGenerated(new GenProductModel { Code = "ABCDE", Quantity = 1234 }));
        }

        [Fact]
        public void FloatColumn_MatchesReflection()
        {
            Assert.Equal(
                WriteOne(new FixedWidthWriter<MeasurementModel>(), new MeasurementModel { Value = 3.14f }),
                WriteGenerated(new GenMeasurementModel { Value = 3.14f }));
        }

        [Fact]
        public void DecimalColumn_MatchesReflection()
        {
            Assert.Equal(
                WriteOne(new FixedWidthWriter<DecimalModel>(), new DecimalModel { Amount = 1234.56m }),
                WriteGenerated(new GenDecimalModel { Amount = 1234.56m }));
        }

        [Fact]
        public void RightAligned_MatchesReflection()
        {
            Assert.Equal(
                WriteOne(new FixedWidthWriter<RightAlignedModel>(), new RightAlignedModel { Value = 30 }),
                WriteGenerated(new GenRightAlignedModel { Value = 30 }));
        }

        [Fact]
        public void ZeroPadded_MatchesReflection()
        {
            Assert.Equal(
                WriteOne(new FixedWidthWriter<ZeroPaddedModel>(), new ZeroPaddedModel { Value = 30 }),
                WriteGenerated(new GenZeroPaddedModel { Value = 30 }));
        }

        [Fact]
        public void FormatString_MatchesReflection()
        {
            Assert.Equal(
                WriteOne(new FixedWidthWriter<FormattedModel>(), new FormattedModel { Amount = 1234.5 }),
                WriteGenerated(new GenFormattedModel { Amount = 1234.5 }));
        }

        [Fact]
        public void Overflow_Throws_MatchesReflection()
        {
            Assert.Throws<InvalidOperationException>(() => WriteOne(new FixedWidthWriter<NarrowModel>(), new NarrowModel { Value = 12345 }));

            var buffer = new byte[16];
            var generatedModel = new GenNarrowModel { Value = 12345 };
            Assert.Throws<InvalidOperationException>(() => FixedWidth.TryFormat(in generatedModel, buffer, Ctx, out _));
        }

        [Fact]
        public void DestinationTooSmall_ReturnsFalseWithZeroBytesWritten()
        {
            var model = new GenPersonModel { Name = "Bob", Age = 1, Salary = 1 };
            Span<byte> tooSmall = stackalloc byte[10];

            bool ok = FixedWidth.TryFormat(in model, tooSmall, Ctx, out int bytesWritten);

            Assert.False(ok);
            Assert.Equal(0, bytesWritten);
        }

        [Fact]
        public void Nullable_Null_MatchesReflection()
        {
            Assert.Equal(
                WriteOne(new FixedWidthWriter<NullableModel>(), new NullableModel { Age = null, Amount = null }),
                WriteGenerated(new GenNullableModel { Age = null, Amount = null }));
        }

        [Fact]
        public void Nullable_Value_MatchesReflection()
        {
            Assert.Equal(
                WriteOne(new FixedWidthWriter<NullableModel>(), new NullableModel { Age = 42, Amount = 123.45m }),
                WriteGenerated(new GenNullableModel { Age = 42, Amount = 123.45m }));
        }

        [Fact]
        public void Converter_MatchesReflection()
        {
            Assert.Equal(
                WriteOne(new FixedWidthWriter<CentsConverterModel>(), new CentsConverterModel { Amount = new CentsValue(12345) }),
                WriteGenerated(new GenCentsConverterModel { Amount = new CentsValue(12345) }));
        }

        [Fact]
        public void NullableConverter_MatchesReflection()
        {
            var writer = new FixedWidthWriter<NullableConverterModel>();

            Assert.Equal(
                WriteOne(writer, new NullableConverterModel { Amount = null }),
                WriteGenerated(new GenNullableConverterModel { Amount = null }));
            Assert.Equal(
                WriteOne(writer, new NullableConverterModel { Amount = new CentsValue(999) }),
                WriteGenerated(new GenNullableConverterModel { Amount = new CentsValue(999) }));
        }

        [Fact]
        public void DecodedParsable_MatchesReflection()
        {
            Assert.Equal(
                WriteOne(new FixedWidthWriter<ShoutModel>(), new ShoutModel { Word = new Shout("HEY") }),
                WriteGenerated(new GenShoutModel { Word = new Shout("HEY") }));
        }

        [Fact]
        public void Culture_HonorsDecimalSeparator_MatchesReflection()
        {
            var deDe = CultureInfo.GetCultureInfo("de-DE");
            var writer = new FixedWidthWriter<PersonModel>(new FixedWidthWriterOptions { FormatProvider = deDe });

            Assert.Equal(
                WriteOne(writer, new PersonModel { Name = "Bob", Age = 30, Salary = 1234.5 }),
                WriteGenerated(new GenPersonModel { Name = "Bob", Age = 30, Salary = 1234.5 }, deDe));
        }
    }
}
