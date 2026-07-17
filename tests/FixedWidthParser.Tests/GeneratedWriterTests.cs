using FixedWidthParser.Writers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Covers the source-generated <c>TryFormat</c> (via <see cref="FixedWidth.TryFormat{TModel}"/>):
    /// parity against the reflection-based <see cref="FixedWidthWriter{TModel}"/>, round-tripping
    /// through the generated <c>TryParse</c>, formatting options (alignment/padding/format/overflow),
    /// nullable columns, custom converters, culture, and the destination-too-small case.
    /// </summary>
    public class GeneratedWriterTests
    {
        private static string WriteGenerated<TModel>(in TModel model, IFormatProvider? formatProvider = null)
            where TModel : IFixedWidthModel<TModel>
        {
            Span<char> buffer = stackalloc char[256];
            bool ok = FixedWidth.TryFormat(in model, buffer, formatProvider ?? Inv, out int written);
            Assert.True(ok);
            return new string(buffer[..written]);
        }

        [Theory]
        [InlineData("John Doe", 30, 60000.00)]
        [InlineData("Alice", 25, 100.50)]
        [InlineData("Max", -5, -1234.56)]
        public void Person_MatchesReflection(string name, int age, double salary)
        {
            var writer = new FixedWidthWriter<PersonModel>();
            var reflectionModel = new PersonModel { Name = name, Age = age, Salary = salary };
            var generatedModel = new GenPersonModel { Name = name, Age = age, Salary = salary };

            string reflectionLine = WriteOne(writer, reflectionModel);
            string generatedLine = WriteGenerated(generatedModel);

            Assert.Equal(reflectionLine, generatedLine);
        }

        [Fact]
        public void Person_RoundTripsThroughGeneratedParse()
        {
            var original = new GenPersonModel { Name = "Bob", Age = 42, Salary = 999.5 };

            string line = WriteGenerated(original);
            bool ok = FixedWidth.TryParse<GenPersonModel>(line, Inv, null, out var parsed);

            Assert.True(ok);
            Assert.Equal(original.Name, parsed.Name);
            Assert.Equal(original.Age, parsed.Age);
            Assert.Equal(original.Salary, parsed.Salary, 2);
        }

        [Fact]
        public void FieldModel_MatchesReflection()
        {
            var writer = new FixedWidthWriter<ProductModel>();
            var reflectionModel = new ProductModel { Code = "ABCDE", Quantity = 1234 };
            var generatedModel = new GenProductModel { Code = "ABCDE", Quantity = 1234 };

            Assert.Equal(WriteOne(writer, reflectionModel), WriteGenerated(generatedModel));
        }

        [Fact]
        public void FloatColumn_MatchesReflection()
        {
            var writer = new FixedWidthWriter<MeasurementModel>();
            var reflectionModel = new MeasurementModel { Value = 3.14f };
            var generatedModel = new GenMeasurementModel { Value = 3.14f };

            Assert.Equal(WriteOne(writer, reflectionModel), WriteGenerated(generatedModel));
        }

        [Fact]
        public void DecimalColumn_MatchesReflection()
        {
            var writer = new FixedWidthWriter<DecimalModel>();
            var reflectionModel = new DecimalModel { Amount = 1234.56m };
            var generatedModel = new GenDecimalModel { Amount = 1234.56m };

            Assert.Equal(WriteOne(writer, reflectionModel), WriteGenerated(generatedModel));
        }

        [Fact]
        public void RightAligned_MatchesReflection()
        {
            var writer = new FixedWidthWriter<RightAlignedModel>();

            Assert.Equal(
                WriteOne(writer, new RightAlignedModel { Value = 30 }),
                WriteGenerated(new GenRightAlignedModel { Value = 30 }));
        }

        [Fact]
        public void ZeroPadded_MatchesReflection()
        {
            var writer = new FixedWidthWriter<ZeroPaddedModel>();

            Assert.Equal(
                WriteOne(writer, new ZeroPaddedModel { Value = 30 }),
                WriteGenerated(new GenZeroPaddedModel { Value = 30 }));
        }

        [Fact]
        public void FormatString_MatchesReflection()
        {
            var writer = new FixedWidthWriter<FormattedModel>();

            Assert.Equal(
                WriteOne(writer, new FormattedModel { Amount = 1234.5 }),
                WriteGenerated(new GenFormattedModel { Amount = 1234.5 }));
        }

        [Fact]
        public void Overflow_Throws_MatchesReflection()
        {
            var writer = new FixedWidthWriter<NarrowModel>();

            Assert.Throws<InvalidOperationException>(() => WriteOne(writer, new NarrowModel { Value = 12345 }));

            var buffer = new char[16];
            var generatedModel = new GenNarrowModel { Value = 12345 };
            Assert.Throws<InvalidOperationException>(() => FixedWidth.TryFormat(in generatedModel, buffer, Inv, out _));
        }

        [Fact]
        public void DestinationTooSmall_ReturnsFalseWithZeroCharsWritten()
        {
            var model = new GenPersonModel { Name = "Bob", Age = 1, Salary = 1 };
            Span<char> tooSmall = stackalloc char[10];

            bool ok = FixedWidth.TryFormat(in model, tooSmall, Inv, out int charsWritten);

            Assert.False(ok);
            Assert.Equal(0, charsWritten);
        }

        // ----- Nullable columns -----

        [Fact]
        public void Nullable_Null_MatchesReflection()
        {
            var writer = new FixedWidthWriter<NullableModel>();
            var reflectionModel = new NullableModel { Age = null, Amount = null };
            var generatedModel = new GenNullableModel { Age = null, Amount = null };

            Assert.Equal(WriteOne(writer, reflectionModel), WriteGenerated(generatedModel));
        }

        [Fact]
        public void Nullable_Value_MatchesReflection()
        {
            var writer = new FixedWidthWriter<NullableModel>();
            var reflectionModel = new NullableModel { Age = 42, Amount = 123.45m };
            var generatedModel = new GenNullableModel { Age = 42, Amount = 123.45m };

            Assert.Equal(WriteOne(writer, reflectionModel), WriteGenerated(generatedModel));
        }

        // ----- Custom converter -----

        [Fact]
        public void Converter_MatchesReflection()
        {
            var writer = new FixedWidthWriter<CentsConverterModel>();
            var reflectionModel = new CentsConverterModel { Amount = new CentsValue(12345) };
            var generatedModel = new GenCentsConverterModel { Amount = new CentsValue(12345) };

            Assert.Equal(WriteOne(writer, reflectionModel), WriteGenerated(generatedModel));
        }

        [Fact]
        public void NullableConverter_Null_MatchesReflection()
        {
            var writer = new FixedWidthWriter<NullableConverterModel>();
            var reflectionModel = new NullableConverterModel { Amount = null };
            var generatedModel = new GenNullableConverterModel { Amount = null };

            Assert.Equal(WriteOne(writer, reflectionModel), WriteGenerated(generatedModel));
        }

        [Fact]
        public void NullableConverter_Value_MatchesReflection()
        {
            var writer = new FixedWidthWriter<NullableConverterModel>();
            var reflectionModel = new NullableConverterModel { Amount = new CentsValue(999) };
            var generatedModel = new GenNullableConverterModel { Amount = new CentsValue(999) };

            Assert.Equal(WriteOne(writer, reflectionModel), WriteGenerated(generatedModel));
        }

        // ----- Culture -----

        [Fact]
        public void Culture_HonorsDecimalSeparator_MatchesReflection()
        {
            var deDe = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
            var writer = new FixedWidthWriter<PersonModel>();
            var reflectionModel = new PersonModel { Name = "Bob", Age = 30, Salary = 1234.5 };
            var generatedModel = new GenPersonModel { Name = "Bob", Age = 30, Salary = 1234.5 };

            Assert.Equal(WriteOne(writer, reflectionModel, deDe), WriteGenerated(generatedModel, deDe));
        }
    }
}
