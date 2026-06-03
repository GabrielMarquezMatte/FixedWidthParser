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

    /// <summary>Modelo com coluna decimal (passa pelo ColumnProcessor genérico, ciente de cultura).</summary>
    public readonly record struct DecimalModel
    {
        public DecimalModel() => Amount = 0m;

        [FixedColumn(0, 12)] public decimal Amount { get; init; }
    }

    /// <summary>Duas colunas string sobrepostas em [2,5), para leitura.</summary>
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

    /// <summary>Duas colunas string sobrepostas em [3,6), para escrita.</summary>
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
}
