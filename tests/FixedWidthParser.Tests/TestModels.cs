using FixedWidthParser.Attributes;

namespace FixedWidthParser.Tests
{
    /// <summary>Modelo baseado em propriedades (string + int + double).</summary>
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

    /// <summary>Modelo com coluna string ao final, para exercitar linha curta.</summary>
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

    /// <summary>Modelo com coluna float.</summary>
    public readonly record struct MeasurementModel
    {
        public MeasurementModel() => Value = 0f;

        [FixedColumn(0, 8)] public float Value { get; init; }
    }

    /// <summary>Modelo baseado em campos públicos (exercita o caminho de FieldInfo).</summary>
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
}
