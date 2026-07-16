using FixedWidthParser.Attributes;

namespace FixedWidthParser.Tests
{
    // Models that opt into the source-generated parser. Each declares BOTH IFixedWidthModel<TSelf>
    // (char) and IUtf8FixedWidthModel<TSelf> (byte), so the generator emits both TryParse overloads
    // into the same partial — exercising marker coexistence. Each mirrors a reflection-based
    // counterpart in TestModels.cs so the parity tests can compare the generated TryParse against the
    // runtime FixedWidthParser<T> / Utf8FixedWidthParser<T> over the same lines.

    /// <summary>Property-based model (string + int + double) — mirrors <see cref="PersonModel"/>.</summary>
    public readonly partial record struct GenPersonModel : IFixedWidthModel<GenPersonModel>, IUtf8FixedWidthModel<GenPersonModel>
    {
        [FixedColumn(0, 10)] public string Name { get; init; }
        [FixedColumn(10, 5)] public int Age { get; init; }
        [FixedColumn(15, 10)] public double Salary { get; init; }
    }

    /// <summary>Public-field model (exercises the field path) — mirrors <see cref="ProductModel"/>.</summary>
    public partial struct GenProductModel : IFixedWidthModel<GenProductModel>, IUtf8FixedWidthModel<GenProductModel>
    {
        [FixedColumn(0, 5)] public string Code;
        [FixedColumn(5, 4)] public int Quantity;
    }

    /// <summary>Float column — mirrors <see cref="MeasurementModel"/>.</summary>
    public readonly partial record struct GenMeasurementModel : IFixedWidthModel<GenMeasurementModel>, IUtf8FixedWidthModel<GenMeasurementModel>
    {
        [FixedColumn(0, 8)] public float Value { get; init; }
    }

    /// <summary>Trailing string column — mirrors <see cref="TrailingStringModel"/>.</summary>
    public readonly partial record struct GenTrailingStringModel : IFixedWidthModel<GenTrailingStringModel>, IUtf8FixedWidthModel<GenTrailingStringModel>
    {
        [FixedColumn(0, 5)] public int Id { get; init; }
        [FixedColumn(5, 10)] public string Note { get; init; }
    }

    /// <summary>Decimal column (ISpanParsable / IUtf8SpanParsable path) — mirrors <see cref="DecimalModel"/>.</summary>
    public readonly partial record struct GenDecimalModel : IFixedWidthModel<GenDecimalModel>, IUtf8FixedWidthModel<GenDecimalModel>
    {
        [FixedColumn(0, 12)] public decimal Amount { get; init; }
    }

    /// <summary>Single-column generated model — mirrors <see cref="CodeModel"/> for reader tests.</summary>
    public readonly partial record struct GenCodeModel : IFixedWidthModel<GenCodeModel>, IUtf8FixedWidthModel<GenCodeModel>
    {
        [FixedColumn(0, 3)] public string Code { get; init; }
    }

    /// <summary>Custom-converter column (<see cref="CentsConverter"/>) — mirrors <see cref="CentsConverterModel"/>.</summary>
    public readonly partial record struct GenCentsConverterModel : IFixedWidthModel<GenCentsConverterModel>, IUtf8FixedWidthModel<GenCentsConverterModel>
    {
        [FixedColumn(0, 8, Converter = typeof(CentsConverter))] public CentsValue Amount { get; init; }
    }

    /// <summary>Nullable value-type columns (T?) — mirrors <see cref="NullableModel"/>.</summary>
    public readonly partial record struct GenNullableModel : IFixedWidthModel<GenNullableModel>, IUtf8FixedWidthModel<GenNullableModel>
    {
        [FixedColumn(0, 5)] public int? Age { get; init; }
        [FixedColumn(5, 10)] public decimal? Amount { get; init; }
    }

    /// <summary>Nullable value-type column + converter — mirrors <see cref="NullableConverterModel"/>.</summary>
    public readonly partial record struct GenNullableConverterModel : IFixedWidthModel<GenNullableConverterModel>, IUtf8FixedWidthModel<GenNullableConverterModel>
    {
        [FixedColumn(0, 8, Converter = typeof(CentsConverter))] public CentsValue? Amount { get; init; }
    }

    /// <summary>Right-aligned integer — mirrors <see cref="RightAlignedModel"/> (write formatting options).</summary>
    public readonly partial record struct GenRightAlignedModel : IFixedWidthModel<GenRightAlignedModel>, IUtf8FixedWidthModel<GenRightAlignedModel>
    {
        [FixedColumn(0, 5, Alignment = Alignment.Right)] public int Value { get; init; }
    }

    /// <summary>Zero-padded right-aligned integer — mirrors <see cref="ZeroPaddedModel"/>.</summary>
    public readonly partial record struct GenZeroPaddedModel : IFixedWidthModel<GenZeroPaddedModel>, IUtf8FixedWidthModel<GenZeroPaddedModel>
    {
        [FixedColumn(0, 5, Alignment = Alignment.Right, Padding = '0')] public int Value { get; init; }
    }

    /// <summary>Double with a fixed format string — mirrors <see cref="FormattedModel"/>.</summary>
    public readonly partial record struct GenFormattedModel : IFixedWidthModel<GenFormattedModel>, IUtf8FixedWidthModel<GenFormattedModel>
    {
        [FixedColumn(0, 8, Format = "F2")] public double Amount { get; init; }
    }

    /// <summary>Narrow numeric column; default overflow (throws) — mirrors <see cref="NarrowModel"/>.</summary>
    public readonly partial record struct GenNarrowModel : IFixedWidthModel<GenNarrowModel>, IUtf8FixedWidthModel<GenNarrowModel>
    {
        [FixedColumn(0, 3)] public int Value { get; init; }
    }

    /// <summary>'*'-padded integer, read with a matching TrimChar — mirrors <see cref="AsteriskTrimIntModel"/>.</summary>
    public readonly partial record struct GenAsteriskTrimIntModel : IFixedWidthModel<GenAsteriskTrimIntModel>, IUtf8FixedWidthModel<GenAsteriskTrimIntModel>
    {
        [FixedColumn(0, 5, TrimChar = '*')] public int Value { get; init; }
    }

    /// <summary>'#'-padded string, read with a matching TrimChar — mirrors <see cref="HashTrimStringModel"/>.</summary>
    public readonly partial record struct GenHashTrimStringModel : IFixedWidthModel<GenHashTrimStringModel>, IUtf8FixedWidthModel<GenHashTrimStringModel>
    {
        [FixedColumn(0, 8, TrimChar = '#')] public string Code { get; init; }
    }

    /// <summary>Non-ASCII TrimChar (U+00A0) — mirrors <see cref="NonAsciiTrimModel"/>; UTF-8 must throw.</summary>
    public readonly partial record struct GenNonAsciiTrimModel : IFixedWidthModel<GenNonAsciiTrimModel>, IUtf8FixedWidthModel<GenNonAsciiTrimModel>
    {
        [FixedColumn(0, 5, TrimChar = ' ')] public int Value { get; init; }
    }

#if NET9_0_OR_GREATER
    /// <summary>Ref struct model — proves generated parsing works under `allows ref struct`. Mirrors <see cref="RefPersonModel"/>.</summary>
    public ref partial struct GenRefPersonModel : IFixedWidthModel<GenRefPersonModel>, IUtf8FixedWidthModel<GenRefPersonModel>
    {
        [FixedColumn(0, 10)] public string Name { get; set; }
        [FixedColumn(10, 5)] public int Age { get; set; }
        [FixedColumn(15, 10)] public double Salary { get; set; }
    }
#endif

    public readonly partial record struct GenDateTimeExactModel : IFixedWidthModel<GenDateTimeExactModel>, IUtf8FixedWidthModel<GenDateTimeExactModel>
    {
        [FixedColumn(0, 8, Format = "yyyyMMdd")] public DateTime Date { get; init; }
        [FixedColumn(8, 8, Format = "yyyyMMdd")] public DateOnly DateOnlyVal { get; init; }
    }

    public readonly partial record struct GenZeroPaddedLeadingModel : IFixedWidthModel<GenZeroPaddedLeadingModel>, IUtf8FixedWidthModel<GenZeroPaddedLeadingModel>
    {
        [FixedColumn(0, 5, TrimChar = '0', TrimMode = TrimMode.Leading)] public int Value { get; init; }
        [FixedColumn(5, 5, TrimChar = '0', TrimMode = TrimMode.Leading)] public double DoubleValue { get; init; }
    }

    public readonly partial record struct GenZeroPaddedBothModel : IFixedWidthModel<GenZeroPaddedBothModel>, IUtf8FixedWidthModel<GenZeroPaddedBothModel>
    {
        [FixedColumn(0, 5, TrimChar = '0', TrimMode = TrimMode.Both)] public int Value { get; init; }
    }

    public readonly partial record struct GenSignPaddedModel : IFixedWidthModel<GenSignPaddedModel>, IUtf8FixedWidthModel<GenSignPaddedModel>
    {
        [FixedColumn(0, 6, Alignment = Alignment.Right, Padding = '0')] public int Value { get; init; }
    }

    public readonly partial record struct GenNullablePaddingModel : IFixedWidthModel<GenNullablePaddingModel>, IUtf8FixedWidthModel<GenNullablePaddingModel>
    {
        [FixedColumn(0, 5, TrimChar = '0')] public int? Value { get; init; }
        [FixedColumn(5, 5, TrimChar = '*')] public double? DoubleValue { get; init; }
    }
}
