using System.Collections.Immutable;
using CommunityToolkit.HighPerformance.Buffers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;

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
            Assert.Empty(result.CompilationErrors);
            var generated = Assert.Single(result.GeneratedSources);
            Assert.Contains("TryParse(global::System.ReadOnlySpan<byte> line, in global::FixedWidthParser.FixedWidthContext context", generated, StringComparison.Ordinal);
            Assert.Contains("if (line.Length < 18) { context.ReportShortLine(18); model = default!; return false; }", generated, StringComparison.Ordinal);
            Assert.Contains("FixedWidthRuntime.String(line.Slice(0, 5), context)", generated, StringComparison.Ordinal);
            Assert.Contains("FixedWidthRuntime.TryParse<int>(line.Slice(5, 3), context, out int __v1)) { context.ReportFailedColumn(\"Quantity\", 5, 3); model = default!; return false; }", generated, StringComparison.Ordinal);
            Assert.Contains("FixedWidthRuntime.TryDouble(line.Slice(8, 5), context", generated, StringComparison.Ordinal);
            Assert.Contains("FixedWidthRuntime.TryFloat(line.Slice(13, 5), context", generated, StringComparison.Ordinal);
            Assert.DoesNotContain("ReadOnlySpan<char>", generated, StringComparison.Ordinal);
            Assert.Contains("Code = __v0", generated, StringComparison.Ordinal);
            Assert.Contains("Discount = __v3", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void ValidModel_GeneratesTryFormat()
        {
            var result = Run("""
                public readonly partial record struct ValidWriteModel : IFixedWidthModel<ValidWriteModel>
                {
                    [FixedColumn(0, 5)] public string Code { get; init; }
                    [FixedColumn(5, 3)] public int Quantity { get; init; }
                }
                """);

            Assert.Empty(result.GeneratorDiagnostics);
            Assert.Empty(result.CompilationErrors);
            var generated = Assert.Single(result.GeneratedSources);
            Assert.Contains("global::System.Span<byte> destination, in global::FixedWidthParser.FixedWidthContext context, out int bytesWritten", generated, StringComparison.Ordinal);
            Assert.Contains("if (destination.Length < 8) { bytesWritten = 0; return false; }", generated, StringComparison.Ordinal);
            Assert.Contains("FixedWidthRuntime.FormatString(model.Code,", generated, StringComparison.Ordinal);
            Assert.Contains("FixedWidthRuntime.FormatUtf8Value(model.Quantity,", generated, StringComparison.Ordinal);
            Assert.Contains("ColumnFormatOptions __options1", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void EveryColumnKind_CompilesCleanly()
        {
            var result = Run("""
                public readonly record struct Cents(long Value);

                public sealed class CentsConverter : FixedWidthParser.IFixedWidthConverter<Cents>
                {
                    public bool TryParse(System.ReadOnlySpan<byte> field, in FixedWidthContext context, out Cents value) { value = default; return true; }
                    public bool TryFormat(Cents value, System.Span<byte> destination, in FixedWidthContext context, out int written) { written = 0; return true; }
                }

                public readonly partial record struct AllKinds : IFixedWidthModel<AllKinds>
                {
                    [FixedColumn(0, 5)] public string Text { get; init; }
                    [FixedColumn(5, 5)] public int? Number { get; init; }
                    [FixedColumn(10, 8, Format = "yyyyMMdd")] public System.DateOnly Day { get; init; }
                    [FixedColumn(18, 8, Converter = typeof(CentsConverter))] public Cents? Amount { get; init; }
                    [FixedColumn(26, 5, TrimChar = '0', TrimMode = TrimMode.Leading)] public decimal Rate { get; init; }
                    [FixedColumn(31, 36)] public System.Guid Id { get; init; }
                }
                """);

            Assert.Empty(result.GeneratorDiagnostics);
            Assert.Empty(result.CompilationErrors);
            var generated = Assert.Single(result.GeneratedSources);
            Assert.Contains("FixedWidthRuntime.IsBlank(line.Slice(5, 5), (byte)' ')", generated, StringComparison.Ordinal);
            Assert.Contains("TryDateOnlyExact(line.Slice(10, 8), \"yyyyMMdd\", context", generated, StringComparison.Ordinal);
            Assert.Contains("(byte)'0', global::FixedWidthParser.TrimMode.Leading", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void CharOnlyParsableColumn_ParsesThroughDecodedText()
        {
            var result = Run("""
                public readonly struct OnlyCharParsable : System.ISpanParsable<OnlyCharParsable>, System.ISpanFormattable
                {
                    public static OnlyCharParsable Parse(System.ReadOnlySpan<char> s, System.IFormatProvider? p) => default;
                    public static bool TryParse(System.ReadOnlySpan<char> s, System.IFormatProvider? p, out OnlyCharParsable r) { r = default; return true; }
                    public static OnlyCharParsable Parse(string s, System.IFormatProvider? p) => default;
                    public static bool TryParse(string? s, System.IFormatProvider? p, out OnlyCharParsable r) { r = default; return true; }
                    public bool TryFormat(System.Span<char> d, out int w, System.ReadOnlySpan<char> f, System.IFormatProvider? p) { w = 0; return true; }
                    public string ToString(string? f, System.IFormatProvider? p) => "";
                }

                public readonly partial record struct DecodedModel : IFixedWidthModel<DecodedModel>
                {
                    [FixedColumn(0, 5)] public OnlyCharParsable Value { get; init; }
                }
                """);

            Assert.Empty(result.GeneratorDiagnostics);
            Assert.Empty(result.CompilationErrors);
            var generated = Assert.Single(result.GeneratedSources);
            Assert.Contains("FixedWidthRuntime.TryParseDecoded<global::GeneratorSmoke.OnlyCharParsable>", generated, StringComparison.Ordinal);
            Assert.Contains("FixedWidthRuntime.FormatValue(model.Value,", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void ColumnNotFormattable_ReportsFwp010_AndEmitsNothing()
        {
            // The interface needs TryFormat too, so a column the writer can't handle blocks the whole emission.
            var result = Run("""
                public readonly struct ParsableNotFormattable : System.ISpanParsable<ParsableNotFormattable>
                {
                    public static ParsableNotFormattable Parse(System.ReadOnlySpan<char> s, System.IFormatProvider? p) => default;
                    public static bool TryParse(System.ReadOnlySpan<char> s, System.IFormatProvider? p, out ParsableNotFormattable r) { r = default; return true; }
                    public static ParsableNotFormattable Parse(string s, System.IFormatProvider? p) => default;
                    public static bool TryParse(string? s, System.IFormatProvider? p, out ParsableNotFormattable r) { r = default; return true; }
                }

                public readonly partial record struct WriteBadModel : IFixedWidthModel<WriteBadModel>
                {
                    [FixedColumn(0, 5)] public ParsableNotFormattable Value { get; init; }
                }
                """);

            Assert.Contains(result.GeneratorDiagnostics, d => string.Equals(d.Id, "FWP010", StringComparison.Ordinal));
            Assert.Empty(result.GeneratedSources);
        }

        [Fact]
        public void ConverterForAnotherType_ReportsFwp008()
        {
            var result = Run("""
                public sealed class IntConverter : FixedWidthParser.IFixedWidthConverter<int>
                {
                    public bool TryParse(System.ReadOnlySpan<byte> field, in FixedWidthContext context, out int value) { value = 0; return true; }
                    public bool TryFormat(int value, System.Span<byte> destination, in FixedWidthContext context, out int written) { written = 0; return true; }
                }

                public readonly partial record struct MismatchModel : IFixedWidthModel<MismatchModel>
                {
                    [FixedColumn(0, 5, Converter = typeof(IntConverter))] public string Value { get; init; }
                }
                """);

            Assert.Contains(result.GeneratorDiagnostics, d => string.Equals(d.Id, "FWP008", StringComparison.Ordinal));
            Assert.Empty(result.GeneratedSources);
        }

        [Theory]
        [InlineData("[FixedColumn(0, 5, Alignment = Alignment.Right, Padding = '\u00e9')] public int Value { get; init; }", "Padding")]
        [InlineData("[FixedColumn(0, 5, TrimChar = '\u00a0')] public int Value { get; init; }", "TrimChar")]
        public void NonAsciiPaddingOrTrimChar_ReportsFwp012(string column, string setting)
        {
            var result = Run("public readonly partial record struct NonAsciiModel : IFixedWidthModel<NonAsciiModel> { " + column + " }");

            var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => string.Equals(d.Id, "FWP012", StringComparison.Ordinal));
            Assert.Contains(setting, diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
            Assert.Empty(result.GeneratedSources);
        }

        [Fact]
        public void Model_IgnoresStaticAndGetOnlyColumns()
        {
            // Static members and get-only properties carrying [FixedColumn] are not settable columns and
            // are silently skipped; only the settable instance column is generated.
            var result = Run("""
                public partial class IgnoringModel : IFixedWidthModel<IgnoringModel>
                {
                    [FixedColumn(0, 5)] public string Code { get; set; }
                    [FixedColumn(5, 5)] public static string Ignored1 { get; set; }
                    [FixedColumn(10, 5)] public string Ignored2 { get; }
                }
                """);

            Assert.Empty(result.GeneratorDiagnostics);
            var generated = Assert.Single(result.GeneratedSources);
            Assert.Contains("Code = __v0", generated, StringComparison.Ordinal);
            Assert.DoesNotContain("Ignored1", generated, StringComparison.Ordinal);
            Assert.DoesNotContain("Ignored2", generated, StringComparison.Ordinal);
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

            Assert.Contains(result.GeneratorDiagnostics, d => string.Equals(d.Id, diagnosticId, StringComparison.Ordinal));
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
            Assert.Contains("ref partial struct RefStructModel", generated, StringComparison.Ordinal);
            Assert.Contains("public static bool TryParse", generated, StringComparison.Ordinal);
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
            Assert.Contains("readonly partial record struct ReadonlyRefRecordStructModel", generated, StringComparison.Ordinal);
            Assert.Contains("public static bool TryParse", generated, StringComparison.Ordinal);
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
            Assert.Contains("partial class ClassModel", generated, StringComparison.Ordinal);
            Assert.DoesNotContain("partial struct", generated, StringComparison.Ordinal);
            Assert.Contains("public static bool TryParse", generated, StringComparison.Ordinal);
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
            Assert.Contains("partial record class RecordClassModel", generated, StringComparison.Ordinal);
            Assert.Contains("public static bool TryParse", generated, StringComparison.Ordinal);
        }

        [Fact]
        public void IncrementalCaching_UnrelatedChange_ReusesModelOutput()
        {
            // Verifies the incremental pipeline caches: an unrelated edit must not re-run source output
            // for an unchanged model. This exercises the value-equality members (ModelInfo/ColumnInfo/
            // EquatableArray Equals + GetHashCode) the framework uses to decide whether to recompute.
            var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
            var compilation = CreateCompilation("""
                public readonly partial record struct CachedModel : IFixedWidthModel<CachedModel>
                {
                    [FixedColumn(0, 5)] public string Code { get; init; }
                    [FixedColumn(5, 3)] public int Quantity { get; init; }
                }
                """, parseOptions);

            var generator = new FixedWidthModelGenerator().AsSourceGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators: [generator],
                additionalTexts: null,
                parseOptions: parseOptions,
                optionsProvider: null,
                driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

            driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
            var first = driver.GetRunResult().Results[0];

            // Add an unrelated type (no base list) so the model's own input is unchanged.
            var updated = compilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText("namespace GeneratorSmoke { internal sealed class Unrelated { } }", parseOptions, cancellationToken: TestContext.Current.CancellationToken));
            driver = driver.RunGenerators(updated, TestContext.Current.CancellationToken);
            var second = driver.GetRunResult().Results[0];

            // Identical output across runs, and the source-output step was served from cache.
            Assert.Equal(
                first.GeneratedSources.Single().SourceText.ToString(),
                second.GeneratedSources.Single().SourceText.ToString());

            var reasons = second.TrackedOutputSteps
                .SelectMany(step => step.Value)
                .SelectMany(run => run.Outputs)
                .Select(output => output.Reason);
            Assert.Contains(IncrementalStepRunReason.Cached, reasons);
        }

        [Fact]
        public void IncrementalCaching_ColumnChange_RegeneratesOutput()
        {
            // The mirror of the cache-reuse test: a real change to a column must invalidate the cache and
            // regenerate, exercising the value-equality members' not-equal branches.
            var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
            var generator = new FixedWidthModelGenerator().AsSourceGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators: [generator],
                additionalTexts: null,
                parseOptions: parseOptions,
                optionsProvider: null,
                driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

            driver = driver.RunGenerators(CreateCompilation("""
                public readonly partial record struct EvolvingModel : IFixedWidthModel<EvolvingModel>
                {
                    [FixedColumn(0, 5)] public string Code { get; init; }
                }
                """, parseOptions), TestContext.Current.CancellationToken);
            var first = driver.GetRunResult().Results[0].GeneratedSources.Single().SourceText.ToString();

            driver = driver.RunGenerators(CreateCompilation("""
                public readonly partial record struct EvolvingModel : IFixedWidthModel<EvolvingModel>
                {
                    [FixedColumn(0, 8)] public string Code { get; init; }
                }
                """, parseOptions), TestContext.Current.CancellationToken);
            var second = driver.GetRunResult().Results[0].GeneratedSources.Single().SourceText.ToString();

            Assert.NotEqual(first, second);
            Assert.Contains("line.Length < 8)", second, StringComparison.Ordinal);
        }

        private static CSharpCompilation CreateCompilation(string modelSource, CSharpParseOptions parseOptions)
        {
            string source = """
                using FixedWidthParser;
                
                namespace GeneratorSmoke;

                """ + modelSource;

            return CSharpCompilation.Create(
                "GeneratorSmoke",
                [CSharpSyntaxTree.ParseText(source, parseOptions)],
                References.Value,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithNullableContextOptions(NullableContextOptions.Enable));
        }

        private static GeneratorRunResult Run(string modelSource)
        {
            string source = """
                using FixedWidthParser;
                
                namespace GeneratorSmoke;

                """ + modelSource;

            var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
            var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions, cancellationToken: TestContext.Current.CancellationToken);
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
                out var updated,
                out var generatorDiagnostics,
                TestContext.Current.CancellationToken);

            var runResult = driver.GetRunResult();
            var generatedSources = runResult.GeneratedTrees
                .Select(t => t.GetText(TestContext.Current.CancellationToken).ToString())
                .ToImmutableArray();
            var compilationErrors = updated.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToImmutableArray();

            return new GeneratorRunResult(generatorDiagnostics, generatedSources, compilationErrors);
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
            ImmutableArray<string> generatedSources,
            ImmutableArray<Diagnostic> compilationErrors)
        {
            public ImmutableArray<Diagnostic> GeneratorDiagnostics { get; } = generatorDiagnostics;
            public ImmutableArray<string> GeneratedSources { get; } = generatedSources;
            public ImmutableArray<Diagnostic> CompilationErrors { get; } = compilationErrors;
        }
    }
}
