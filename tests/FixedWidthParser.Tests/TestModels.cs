using FixedWidthParser.Attributes;

namespace FixedWidthParser.Tests
{
    /// <summary>Property-based model (string + int + double).</summary>
    public readonly record struct PersonModel
    {
        public PersonModel()
        {
            Name = string.Empty;
            Age = 0;
            Salary = 0.0;
        }

        [FixedColumn(0, 10)] public string Name { get; init; }
        [FixedColumn(10, 5)] public int Age { get; init; }
        [FixedColumn(15, 10)] public double Salary { get; init; }
    }

    /// <summary>Model with a trailing string column, to exercise short lines.</summary>
    public readonly record struct TrailingStringModel
    {
        public TrailingStringModel()
        {
            Id = 0;
            Note = string.Empty;
        }

        [FixedColumn(0, 5)] public int Id { get; init; }
        [FixedColumn(5, 10)] public string Note { get; init; }
    }

    /// <summary>Model with a float column.</summary>
    public readonly record struct MeasurementModel
    {
        public MeasurementModel() => Value = 0f;

        [FixedColumn(0, 8)] public float Value { get; init; }
    }

    /// <summary>Model based on public fields (exercises the FieldInfo path).</summary>
    public struct ProductModel
    {
        public ProductModel()
        {
            Code = string.Empty;
            Quantity = 0;
        }

        [FixedColumn(0, 5)] public string Code;
        [FixedColumn(5, 4)] public int Quantity;
    }

    /// <summary>Integer right-aligned with spaces.</summary>
    public readonly record struct RightAlignedModel
    {
        public RightAlignedModel() => Value = 0;

        [FixedColumn(0, 5, Alignment = Alignment.Right)] public int Value { get; init; }
    }

    /// <summary>Integer zero-padded on the right.</summary>
    public readonly record struct ZeroPaddedModel
    {
        public ZeroPaddedModel() => Value = 0;

        [FixedColumn(0, 5, Alignment = Alignment.Right, Padding = '0')] public int Value { get; init; }
    }

    /// <summary>Double with a fixed format string ("F2").</summary>
    public readonly record struct FormattedModel
    {
        public FormattedModel() => Amount = 0;

        [FixedColumn(0, 8, Format = "F2")] public double Amount { get; init; }
    }

    /// <summary>Narrow numeric column; default overflow (throws).</summary>
    public readonly record struct NarrowModel
    {
        public NarrowModel() => Value = 0;

        [FixedColumn(0, 3)] public int Value { get; init; }
    }

    /// <summary>Narrow numeric column; opt-in overflow truncation.</summary>
    public readonly record struct NarrowTruncateModel
    {
        public NarrowTruncateModel() => Value = 0;

        [FixedColumn(0, 3, Overflow = OverflowBehavior.Truncate)] public int Value { get; init; }
    }

    /// <summary>Right-aligned string.</summary>
    public readonly record struct RightStringModel
    {
        public RightStringModel() => Code = string.Empty;

        [FixedColumn(0, 6, Alignment = Alignment.Right)] public string Code { get; init; }
    }

    /// <summary>Ref struct model — exercises the parser's allows ref struct constraint.</summary>
    public ref struct RefPersonModel
    {
        public RefPersonModel()
        {
            Name = string.Empty;
            Age = 0;
            Salary = 0.0;
        }

        [FixedColumn(0, 10)] public string Name { get; set; }
        [FixedColumn(10, 5)] public int Age { get; set; }
        [FixedColumn(15, 10)] public double Salary { get; set; }
    }

    /// <summary>Single-column model, convenient for the reader tests.</summary>
    public readonly record struct CodeModel
    {
        public CodeModel() => Code = string.Empty;

        [FixedColumn(0, 3)] public string Code { get; init; }
    }

    /// <summary>Model with a decimal column (goes through the generic, culture-aware ColumnProcessor).</summary>
    public readonly record struct DecimalModel
    {
        public DecimalModel() => Amount = 0m;

        [FixedColumn(0, 12)] public decimal Amount { get; init; }
    }

    /// <summary>Two columns overlapping at [2,5) — invalid layout (must fail on construction).</summary>
    public readonly record struct OverlapReadModel
    {
        public OverlapReadModel()
        {
            Left = string.Empty;
            Right = string.Empty;
        }

        [FixedColumn(0, 5)] public string Left { get; init; }
        [FixedColumn(2, 5)] public string Right { get; init; }
    }

    /// <summary>Two columns overlapping at [3,6) — invalid layout (must fail on construction).</summary>
    public readonly record struct OverlapWriteModel
    {
        public OverlapWriteModel()
        {
            Left = string.Empty;
            Right = string.Empty;
        }

        [FixedColumn(0, 6)] public string Left { get; init; }
        [FixedColumn(3, 6)] public string Right { get; init; }
    }

    /// <summary>Negative Start — invalid layout.</summary>
    public readonly record struct NegativeStartModel
    {
        public NegativeStartModel() => Value = string.Empty;

        [FixedColumn(-1, 5)] public string Value { get; init; }
    }

    /// <summary>Zero Length — invalid layout.</summary>
    public readonly record struct ZeroLengthModel
    {
        public ZeroLengthModel() => Value = string.Empty;

        [FixedColumn(0, 0)] public string Value { get; init; }
    }

    /// <summary>Adjacent but non-overlapping columns — valid layout (edge case).</summary>
    public readonly record struct AdjacentColumnsModel
    {
        public AdjacentColumnsModel()
        {
            First = string.Empty;
            Second = string.Empty;
        }

        [FixedColumn(0, 5)] public string First { get; init; }
        [FixedColumn(5, 5)] public string Second { get; init; }
    }
}
