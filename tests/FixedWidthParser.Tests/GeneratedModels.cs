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

#if NET9_0_OR_GREATER
    /// <summary>Ref struct model — proves generated parsing works under `allows ref struct`. Mirrors <see cref="RefPersonModel"/>.</summary>
    public ref partial struct GenRefPersonModel : IFixedWidthModel<GenRefPersonModel>, IUtf8FixedWidthModel<GenRefPersonModel>
    {
        [FixedColumn(0, 10)] public string Name { get; set; }
        [FixedColumn(10, 5)] public int Age { get; set; }
        [FixedColumn(15, 10)] public double Salary { get; set; }
    }
#endif
}
