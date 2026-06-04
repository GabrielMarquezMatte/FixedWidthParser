using System.Collections.Immutable;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FixedWidthParser.Generator.Tests
{
    public class FixedWidthModelGeneratorTests
    {
        [Fact]
        public void ValidModel_GeneratesTryParse()
        {
            var result = Run("""
                public readonly partial record struct ValidModel : IFixedWidthModel<ValidModel>
                {
                    [FixedColumn(0, 5)] public string Code { get; init; }
                    [FixedColumn(5, 3)] public int Quantity { get; init; }
                }
                """);

            Assert.Empty(result.GeneratorDiagnostics);
            var generated = Assert.Single(result.GeneratedSources);
            Assert.Contains("public static bool TryParse", generated);
            Assert.Contains("Code = __v0", generated);
            Assert.Contains("Quantity = __v1", generated);
        }

        [Theory]
        [InlineData("""
            public readonly record struct BadModel : IFixedWidthModel<BadModel>
            {
                [FixedColumn(0, 5)] public string Code { get; init; }
            }
            """, "FWP001")]
        [InlineData("""
            public partial class Outer
            {
                public readonly partial record struct BadModel : IFixedWidthModel<BadModel>
                {
                    [FixedColumn(0, 5)] public string Code { get; init; }
                }
            }
            """, "FWP002")]
        [InlineData("""
            public readonly partial record struct BadModel : IFixedWidthModel<BadModel>
            {
                [FixedColumn(0, 5)] public object Value { get; init; }
            }
            """, "FWP003")]
        [InlineData("""
            public readonly partial record struct BadModel : IFixedWidthModel<BadModel>
            {
                [FixedColumn(-1, 5)] public string Value { get; init; }
            }
            """, "FWP004")]
        [InlineData("""
            public readonly partial record struct BadModel : IFixedWidthModel<BadModel>
            {
                [FixedColumn(0, 0)] public string Value { get; init; }
            }
            """, "FWP005")]
        [InlineData("""
            public readonly partial record struct BadModel : IFixedWidthModel<BadModel>
            {
                [FixedColumn(0, 5)] public string Left { get; init; }
                [FixedColumn(2, 5)] public string Right { get; init; }
            }
            """, "FWP006")]
        public void InvalidModel_ReportsExpectedDiagnostic(string modelSource, string diagnosticId)
        {
            var result = Run(modelSource);

            Assert.Contains(result.GeneratorDiagnostics, d => d.Id == diagnosticId);
            Assert.Empty(result.GeneratedSources);
        }

        [Fact]
        public void AdjacentColumns_AreValid()
        {
            var result = Run("""
                public readonly partial record struct AdjacentModel : IFixedWidthModel<AdjacentModel>
                {
                    [FixedColumn(0, 5)] public string First { get; init; }
                    [FixedColumn(5, 5)] public string Second { get; init; }
                }
                """);

            Assert.Empty(result.GeneratorDiagnostics);
            Assert.Single(result.GeneratedSources);
        }

        [Fact]
        public void ModelWithoutColumns_IsValid()
        {
            var result = Run("""
                public readonly partial record struct EmptyModel : IFixedWidthModel<EmptyModel>
                {
                }
                """);

            Assert.Empty(result.GeneratorDiagnostics);
            Assert.Single(result.GeneratedSources);
        }

        private static GeneratorRunResult Run(string modelSource)
        {
            string source = """
                using FixedWidthParser;
                using FixedWidthParser.Attributes;

                namespace GeneratorSmoke;

                """ + modelSource;

            var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
            var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
            var compilation = CSharpCompilation.Create(
                "GeneratorSmoke",
                [syntaxTree],
                References.Value,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithNullableContextOptions(NullableContextOptions.Enable));

            var generator = new FixedWidthModelGenerator().AsSourceGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create([generator], parseOptions: parseOptions);
            driver = driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out _,
                out var generatorDiagnostics);

            var runResult = driver.GetRunResult();
            var generatedSources = runResult.GeneratedTrees
                .Select(t => t.GetText().ToString())
                .ToImmutableArray();

            return new GeneratorRunResult(generatorDiagnostics, generatedSources);
        }

        private static readonly Lazy<ImmutableArray<MetadataReference>> References = new(CreateReferences);

        private static ImmutableArray<MetadataReference> CreateReferences()
        {
            var builder = ImmutableArray.CreateBuilder<MetadataReference>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string path)
            {
                if (!string.IsNullOrWhiteSpace(path) && seen.Add(path))
                {
                    builder.Add(MetadataReference.CreateFromFile(path));
                }
            }

            var trustedAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            if (trustedAssemblies is not null)
            {
                foreach (var path in trustedAssemblies.Split(Path.PathSeparator))
                {
                    Add(path);
                }
            }

            Add(typeof(IFixedWidthModel<>).Assembly.Location);
            Add(typeof(StringPool).Assembly.Location);

            return builder.ToImmutable();
        }

        private sealed class GeneratorRunResult(
            ImmutableArray<Diagnostic> generatorDiagnostics,
            ImmutableArray<string> generatedSources)
        {
            public ImmutableArray<Diagnostic> GeneratorDiagnostics { get; } = generatorDiagnostics;
            public ImmutableArray<string> GeneratedSources { get; } = generatedSources;
        }
    }
}
