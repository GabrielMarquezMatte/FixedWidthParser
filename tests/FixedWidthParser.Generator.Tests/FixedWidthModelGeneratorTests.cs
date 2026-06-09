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
                    [FixedColumn(8, 5)] public double Price { get; init; }
                    [FixedColumn(13, 5)] public float Discount { get; init; }
                }
                """);

            Assert.Empty(result.GeneratorDiagnostics);
            var generated = Assert.Single(result.GeneratedSources);
            Assert.Contains("public static bool TryParse", generated);
            Assert.Contains("if (line.Length < 18) { model = default!; return false; }", generated);
            Assert.Contains("FixedWidthRuntime.String(line.Slice(0, 5), stringPool)", generated);
            Assert.Contains("FixedWidthRuntime.TryParse<int>(line.Slice(5, 3), formatProvider", generated);
            Assert.Contains("FixedWidthRuntime.TryDouble(line.Slice(8, 5), formatProvider", generated);
            Assert.Contains("FixedWidthRuntime.TryFloat(line.Slice(13, 5), formatProvider", generated);
            Assert.DoesNotContain("global::System.Math.Min", generated);
            Assert.DoesNotContain("FixedWidthRuntime.Column", generated);
            Assert.Contains("Code = __v0", generated);
            Assert.Contains("Quantity = __v1", generated);
            Assert.Contains("Price = __v2", generated);
            Assert.Contains("Discount = __v3", generated);
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

        [Fact]
        public void ValidModel_RefStruct_GeneratesRefPartialStruct()
        {
            var result = Run("""
                public ref partial struct RefStructModel : IFixedWidthModel<RefStructModel>
                {
                    [FixedColumn(0, 5)] public string Code { get; set; }
                    [FixedColumn(5, 3)] public int Quantity { get; set; }
                }
                """);

            Assert.Empty(result.GeneratorDiagnostics);
            var generated = Assert.Single(result.GeneratedSources);
            Assert.Contains("ref partial struct RefStructModel", generated);
            Assert.Contains("public static bool TryParse", generated);
        }

        [Fact]
        public void ValidModel_ReadonlyRefRecordStruct_GeneratesReadonlyRefPartialRecordStruct()
        {
            var result = Run("""
                public readonly partial record struct ReadonlyRefRecordStructModel : IFixedWidthModel<ReadonlyRefRecordStructModel>
                {
                    [FixedColumn(0, 5)] public string Code { get; init; }
                    [FixedColumn(5, 3)] public int Quantity { get; init; }
                }
                """);

            Assert.Empty(result.GeneratorDiagnostics);
            var generated = Assert.Single(result.GeneratedSources);
            Assert.Contains("readonly partial record struct ReadonlyRefRecordStructModel", generated);
            Assert.Contains("public static bool TryParse", generated);
        }

        [Fact]
        public void ValidModel_Class_GeneratesPartialClass()
        {
            var result = Run("""
                public partial class ClassModel : IFixedWidthModel<ClassModel>
                {
                    [FixedColumn(0, 5)] public string Code { get; set; }
                    [FixedColumn(5, 3)] public int Quantity { get; set; }
                }
                """);

            Assert.Empty(result.GeneratorDiagnostics);
            var generated = Assert.Single(result.GeneratedSources);
            Assert.Contains("partial class ClassModel", generated);
            Assert.DoesNotContain("partial struct", generated);
            Assert.Contains("public static bool TryParse", generated);
        }

        [Fact]
        public void ValidModel_RecordClass_GeneratesPartialRecordClass()
        {
            var result = Run("""
                public partial record RecordClassModel : IFixedWidthModel<RecordClassModel>
                {
                    [FixedColumn(0, 5)] public string Code { get; init; }
                    [FixedColumn(5, 3)] public int Quantity { get; init; }
                }
                """);

            Assert.Empty(result.GeneratorDiagnostics);
            var generated = Assert.Single(result.GeneratedSources);
            Assert.Contains("partial record class RecordClassModel", generated);
            Assert.Contains("public static bool TryParse", generated);
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
