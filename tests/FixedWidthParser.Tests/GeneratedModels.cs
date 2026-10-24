namespace FixedWidthParser.Tests
{
    // Models that opt into the source-generated parser and writer. Each mirrors a reflection-based
    // counterpart in TestModels.cs so the parity tests can compare the generated TryParse/TryFormat
    // against the same type's attribute layout (FixedWidthLayout.FromAttributes) over the same lines.

    /// <summary>Property-based model (string + int + double) — mirrors <see cref="PersonModel"/>.</summary>
    public readonly partial record struct GenPersonModel : IFixedWidthModel<GenPersonModel>
    {
        [FixedColumn(0, 10)] public string Name { get; init; }
        [FixedColumn(10, 5)] public int Age { get; init; }
        [FixedColumn(15, 10)] public double Salary { get; init; }
    }

    /// <summary>Public-field model (exercises the field path) — mirrors <see cref="ProductModel"/>.</summary>
    public partial struct GenProductModel : IFixedWidthModel<GenProductModel>
    {
        [FixedColumn(0, 5)] public string Code;
        [FixedColumn(5, 4)] public int Quantity;
    }

    /// <summary>Float column — mirrors <see cref="MeasurementModel"/>.</summary>
    public readonly partial record struct GenMeasurementModel : IFixedWidthModel<GenMeasurementModel>
    {
        [FixedColumn(0, 8)] public float Value { get; init; }
    }

    /// <summary>Trailing string column — mirrors <see cref="TrailingStringModel"/>.</summary>
    public readonly partial record struct GenTrailingStringModel : IFixedWidthModel<GenTrailingStringModel>
    {
        [FixedColumn(0, 5)] public int Id { get; init; }
        [FixedColumn(5, 10)] public string Note { get; init; }
    }

    /// <summary>Decimal column (IUtf8SpanParsable path) — mirrors <see cref="DecimalModel"/>.</summary>
    public readonly partial record struct GenDecimalModel : IFixedWidthModel<GenDecimalModel>
    {
        [FixedColumn(0, 12)] public decimal Amount { get; init; }
    }

    /// <summary>Single-column generated model — mirrors <see cref="CodeModel"/> for reader tests.</summary>
    public readonly partial record struct GenCodeModel : IFixedWidthModel<GenCodeModel>
    {
        [FixedColumn(0, 3)] public string Code { get; init; }
    }

    /// <summary>Custom-converter column (<see cref="CentsConverter"/>) — mirrors <see cref="CentsConverterModel"/>.</summary>
    public readonly partial record struct GenCentsConverterModel : IFixedWidthModel<GenCentsConverterModel>
    {
        [FixedColumn(0, 8, Converter = typeof(CentsConverter))] public CentsValue Amount { get; init; }
    }

    /// <summary>Nullable value-type columns (T?) — mirrors <see cref="NullableModel"/>.</summary>
    public readonly partial record struct GenNullableModel : IFixedWidthModel<GenNullableModel>
    {
        [FixedColumn(0, 5)] public int? Age { get; init; }
        [FixedColumn(5, 10)] public decimal? Amount { get; init; }
    }

    /// <summary>Nullable value-type column + converter — mirrors <see cref="NullableConverterModel"/>.</summary>
    public readonly partial record struct GenNullableConverterModel : IFixedWidthModel<GenNullableConverterModel>
    {
        [FixedColumn(0, 8, Converter = typeof(CentsConverter))] public CentsValue? Amount { get; init; }
    }

    /// <summary>Right-aligned integer — mirrors <see cref="RightAlignedModel"/>.</summary>
    public readonly partial record struct GenRightAlignedModel : IFixedWidthModel<GenRightAlignedModel>
    {
        [FixedColumn(0, 5, Alignment = Alignment.Right)] public int Value { get; init; }
    }

    /// <summary>Zero-padded right-aligned integer — mirrors <see cref="ZeroPaddedModel"/>.</summary>
    public readonly partial record struct GenZeroPaddedModel : IFixedWidthModel<GenZeroPaddedModel>
    {
        [FixedColumn(0, 5, Alignment = Alignment.Right, Padding = '0')] public int Value { get; init; }
    }

    /// <summary>Double with a fixed format string — mirrors <see cref="FormattedModel"/>.</summary>
    public readonly partial record struct GenFormattedModel : IFixedWidthModel<GenFormattedModel>
    {
        [FixedColumn(0, 8, Format = "F2")] public double Amount { get; init; }
    }

    /// <summary>Narrow numeric column; default overflow (throws) — mirrors <see cref="NarrowModel"/>.</summary>
    public readonly partial record struct GenNarrowModel : IFixedWidthModel<GenNarrowModel>
    {
        [FixedColumn(0, 3)] public int Value { get; init; }
    }

    /// <summary>'*'-padded integer, read with a matching TrimChar — mirrors <see cref="AsteriskTrimIntModel"/>.</summary>
    public readonly partial record struct GenAsteriskTrimIntModel : IFixedWidthModel<GenAsteriskTrimIntModel>
    {
        [FixedColumn(0, 5, TrimChar = '*')] public int Value { get; init; }
    }

    /// <summary>'#'-padded string, read with a matching TrimChar — mirrors <see cref="HashTrimStringModel"/>.</summary>
    public readonly partial record struct GenHashTrimStringModel : IFixedWidthModel<GenHashTrimStringModel>
    {
        [FixedColumn(0, 8, TrimChar = '#')] public string Code { get; init; }
    }

    /// <summary>Ref struct model — proves generated parsing works under `allows ref struct`. Mirrors <see cref="RefPersonModel"/>.</summary>
    public ref partial struct GenRefPersonModel : IFixedWidthModel<GenRefPersonModel>
    {
        [FixedColumn(0, 10)] public string Name { get; set; }
        [FixedColumn(10, 5)] public int Age { get; set; }
        [FixedColumn(15, 10)] public double Salary { get; set; }
    }

    public readonly partial record struct GenDateTimeExactModel : IFixedWidthModel<GenDateTimeExactModel>
    {
        [FixedColumn(0, 8, Format = "yyyyMMdd")] public DateTime Date { get; init; }
        [FixedColumn(8, 8, Format = "yyyyMMdd")] public DateOnly DateOnlyVal { get; init; }
    }

    /// <summary>TimeOnly + DateTimeOffset exact columns — mirrors <see cref="TimeExactModel"/>.</summary>
    public readonly partial record struct GenTimeExactModel : IFixedWidthModel<GenTimeExactModel>
    {
        [FixedColumn(0, 6, Format = "HHmmss")] public TimeOnly Time { get; init; }
        [FixedColumn(6, 20, Format = "yyyyMMddHHmmsszzz")] public DateTimeOffset Timestamp { get; init; }
    }

    public readonly partial record struct GenZeroPaddedLeadingModel : IFixedWidthModel<GenZeroPaddedLeadingModel>
    {
        [FixedColumn(0, 5, TrimChar = '0', TrimMode = TrimMode.Leading)] public int Value { get; init; }
        [FixedColumn(5, 5, TrimChar = '0', TrimMode = TrimMode.Leading)] public double DoubleValue { get; init; }
    }

    public readonly partial record struct GenZeroPaddedBothModel : IFixedWidthModel<GenZeroPaddedBothModel>
    {
        [FixedColumn(0, 5, TrimChar = '0', TrimMode = TrimMode.Both)] public int Value { get; init; }
    }

    public readonly partial record struct GenSignPaddedModel : IFixedWidthModel<GenSignPaddedModel>
    {
        [FixedColumn(0, 6, Alignment = Alignment.Right, Padding = '0')] public int Value { get; init; }
    }

    public readonly partial record struct GenNullablePaddingModel : IFixedWidthModel<GenNullablePaddingModel>
    {
        [FixedColumn(0, 5, TrimChar = '0')] public int? Value { get; init; }
        [FixedColumn(5, 5, TrimChar = '*')] public double? DoubleValue { get; init; }
    }

    /// <summary>Character-only parsable column — mirrors <see cref="ShoutModel"/>.</summary>
    public readonly partial record struct GenShoutModel : IFixedWidthModel<GenShoutModel>
    {
        [FixedColumn(0, 6)] public Shout Word { get; init; }
    }

    /// <summary>Latin-1 name and number — mirrors <see cref="NameAgeModel"/>.</summary>
    public readonly partial record struct GenNameAgeModel : IFixedWidthModel<GenNameAgeModel>
    {
        [FixedColumn(0, 6)] public string Name { get; init; }
        [FixedColumn(6, 3)] public int Age { get; init; }
    }

    /// <summary>Currency column — mirrors <see cref="PriceModel"/>.</summary>
    public readonly partial record struct GenPriceModel : IFixedWidthModel<GenPriceModel>
    {
        [FixedColumn(0, 8, Alignment = Alignment.Right, Format = "C")] public decimal Price { get; init; }
    }

    /// <summary>A line longer than the single-record stack buffer.</summary>
    public readonly partial record struct GenLongNameModel : IFixedWidthModel<GenLongNameModel>
    {
        [FixedColumn(0, 1200)] public string Name { get; init; }
    }

    /// <summary>A line longer than a whole write batch.</summary>
    public readonly partial record struct GenHugeNameModel : IFixedWidthModel<GenHugeNameModel>
    {
        [FixedColumn(0, 70_000)] public string Name { get; init; }
    }
}
