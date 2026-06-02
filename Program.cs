using BenchmarkDotNet.Running;
using Benchmarks.Attributes;
using Benchmarks.Parsers;

namespace Benchmarks
{
    public static class Program
    {
        private readonly ref struct Teste
        {
            [FixedColumn(0, 10)]
            public readonly string? Nome;
            [FixedColumn(10, 5)]
            public readonly int Idade;
            [FixedColumn(15, 10)]
            public readonly double Salario;
            [FixedColumn(25, 7)]
            public readonly float Bonus;
            [FixedColumn(32, 10)]
            public readonly string? Departamento;
            [FixedColumn(42, 10)]
            public readonly string? Cargo;
            [FixedColumn(52, 36)]
            public readonly Guid Id;
            public Teste()
            {
                Nome = null;
                Idade = 0;
                Salario = 0;
                Bonus = 0;
                Departamento = null;
                Cargo = null;
                Id = Guid.Empty;
            }
        }
        public static void Main(string[] args)
        {
            FixedWidthParser<Teste> teste = new();
            const string line = "John Doe  30   50000.00  500.0  Sales     Manager   123e4567-e89b-12d3-a456-426614174000";
            if (teste.TryParse(line, null, null, out var model))
            {
                Console.WriteLine($"Nome: {model.Nome}, Idade: {model.Idade}, Salário: {model.Salario}, Bônus: {model.Bonus}, Departamento: {model.Departamento}, Cargo: {model.Cargo}, Id: {model.Id}");
            }
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
