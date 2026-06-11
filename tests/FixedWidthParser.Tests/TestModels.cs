using System.Diagnostics.CodeAnalysis;
using FixedWidthParser.Attributes;

namespace FixedWidthParser.Tests
{
    /// <summary>Property-based model (string + int + double).</summary>
    public readonly record struct PersonModel
    {
        [FixedColumn(0, 10)] public string Name { get; init; }
        [FixedColumn(10, 5)] public int Age { get; init; }
        [FixedColumn(15, 10)] public double Salary { get; init; }
    }

    /// <summary>Model with a trailing string column, to exercise short lines.</summary>
    public readonly record struct TrailingStringModel
    {
        [FixedColumn(0, 5)] public int Id { get; init; }
        [FixedColumn(5, 10)] public string Note { get; init; }
    }

    /// <summary>Model with a float column.</summary>
    public readonly record struct MeasurementModel
    {
        [FixedColumn(0, 8)] public float Value { get; init; }
    }

    /// <summary>Model based on public fields (exercises the FieldInfo path).</summary>
    public struct ProductModel
    {
        [FixedColumn(0, 5)] public string Code;
        [FixedColumn(5, 4)] public int Quantity;
    }

    /// <summary>Integer right-aligned with spaces.</summary>
    public readonly record struct RightAlignedModel
    {
        [FixedColumn(0, 5, Alignment = Alignment.Right)] public int Value { get; init; }
    }

    /// <summary>Integer zero-padded on the right.</summary>
    public readonly record struct ZeroPaddedModel
    {
        [FixedColumn(0, 5, Alignment = Alignment.Right, Padding = '0')] public int Value { get; init; }
    }

    /// <summary>Double with a fixed format string ("F2").</summary>
    public readonly record struct FormattedModel
    {
        [FixedColumn(0, 8, Format = "F2")] public double Amount { get; init; }
    }

    /// <summary>Narrow numeric column; default overflow (throws).</summary>
    public readonly record struct NarrowModel
    {
        [FixedColumn(0, 3)] public int Value { get; init; }
    }

    /// <summary>Narrow numeric column; opt-in overflow truncation.</summary>
    public readonly record struct NarrowTruncateModel
    {
        [FixedColumn(0, 3, Overflow = OverflowBehavior.Truncate)] public int Value { get; init; }
    }

    /// <summary>Right-aligned string.</summary>
    public readonly record struct RightStringModel
    {
        [FixedColumn(0, 6, Alignment = Alignment.Right)] public string Code { get; init; }
    }

    /// <summary>Ref struct model — exercises the parser's allows ref struct constraint.</summary>
    public ref struct RefPersonModel
    {
        [FixedColumn(0, 10)] public string Name { get; set; }
        [FixedColumn(10, 5)] public int Age { get; set; }
        [FixedColumn(15, 10)] public double Salary { get; set; }
    }

    /// <summary>Single-column model, convenient for the reader tests.</summary>
    public readonly record struct CodeModel
    {
        [FixedColumn(0, 3)] public string Code { get; init; }
    }

    /// <summary>Model with a decimal column (goes through the ISpanParsable fallback value parser).</summary>
    public readonly record struct DecimalModel
    {
        [FixedColumn(0, 12)] public decimal Amount { get; init; }
    }

    /// <summary>Two columns overlapping at [2,5) — invalid layout (must fail on construction).</summary>
    public readonly record struct OverlapReadModel
    {

        [FixedColumn(0, 5)] public string Left { get; init; }
        [FixedColumn(2, 5)] public string Right { get; init; }
    }

    /// <summary>Two columns overlapping at [3,6) — invalid layout (must fail on construction).</summary>
    public readonly record struct OverlapWriteModel
    {
        [FixedColumn(0, 6)] public string Left { get; init; }
        [FixedColumn(3, 6)] public string Right { get; init; }
    }

    /// <summary>Negative Start — invalid layout.</summary>
    public readonly record struct NegativeStartModel
    {
        [FixedColumn(-1, 5)] public string Value { get; init; }
    }

    /// <summary>Zero Length — invalid layout.</summary>
    public readonly record struct ZeroLengthModel
    {
        [FixedColumn(0, 0)] public string Value { get; init; }
    }

    /// <summary>
    /// Value type WITHOUT an explicitly declared parameterless constructor. It still satisfies the
    /// <c>new()</c> constraint (all structs do), even though
    /// <c>typeof(T).GetConstructor(Type.EmptyTypes)</c> returns <see langword="null"/> for such a
    /// struct — the parser's <c>BuildModelFactory</c> must default-initialize it instead of throwing.
    /// </summary>
    public struct NoParameterlessCtorModel
    {
        [FixedColumn(0, 3)] public string Code;
    }

    /// <summary>No <see cref="FixedColumnAttribute"/> members — invalid layout (must fail on construction).</summary>
    public struct NoColumnsModel
    {
        [ExcludeFromCodeCoverage]
        public int Ignored { get; set; }
    }

    /// <summary>Adjacent but non-overlapping columns — valid layout (edge case).</summary>
    public readonly record struct AdjacentColumnsModel
    {
        [FixedColumn(0, 5)] public string First { get; init; }
        [FixedColumn(5, 5)] public string Second { get; init; }
    }

    /// <summary>
    /// ISpanFormattable that emits a configurable number of 'X' characters. Used to force the
    /// writer's ArrayPool fallback when the formatted text exceeds the stack buffer.
    /// </summary>
    public readonly struct RepeatedChar(int count) : ISpanFormattable
    {
        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            if (destination.Length < count)
            {
                charsWritten = 0;
                return false;
            }
            destination[..count].Fill('X');
            charsWritten = count;
            return true;
        }

        [ExcludeFromCodeCoverage]
        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            return new('X', count);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return new('X', count);
        }
    }

    /// <summary>Single column of a value type that can format wider than the stack buffer.</summary>
    public readonly record struct WideValueModel
    {
        [FixedColumn(0, 4, Overflow = OverflowBehavior.Truncate)] public RepeatedChar Value { get; init; }
    }

    /// <summary>Right-aligned column that truncates on overflow (keeps the rightmost characters).</summary>
    public readonly record struct RightTruncateModel
    {
        [FixedColumn(0, 4, Alignment = Alignment.Right, Overflow = OverflowBehavior.Truncate)] public string Code { get; init; }
    }

    /// <summary>
    /// Line longer than 1024 chars, so the writer takes its <see cref="System.Buffers.ArrayPool{T}"/>
    /// path instead of the <c>stackalloc</c> fast path.
    /// </summary>
    public readonly record struct WideLineModel
    {
        public const int LineLength = 1208;
        [FixedColumn(0, 1200)] public string Name { get; init; }
        [FixedColumn(1200, 8)] public int Number { get; init; }
    }
}
