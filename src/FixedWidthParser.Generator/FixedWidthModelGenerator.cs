using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FixedWidthParser.Generator
{
    /// <summary>
    /// Emits a reflection-free <c>TryParse</c> for every <see langword="partial"/> type that declares
    /// <c>FixedWidthParser.IFixedWidthModel&lt;TSelf&gt;</c>, mirroring the runtime parser's semantics.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class FixedWidthModelGenerator : IIncrementalGenerator
    {
        private const string MarkerMetadataName = "FixedWidthParser.IFixedWidthModel`1";
        private const string ColumnAttributeMetadataName = "FixedWidthParser.Attributes.FixedColumnAttribute";

        private static readonly DiagnosticDescriptor MustBePartial = new(
            "FWP001", "Fixed-width model must be partial",
            "Type '{0}' implements IFixedWidthModel but is not declared 'partial', so the parser cannot be generated",
            "FixedWidthParser", DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor MustBeTopLevel = new(
            "FWP002", "Fixed-width model must be top-level",
            "Type '{0}' is nested; generated fixed-width parsing currently supports only non-nested types",
            "FixedWidthParser", DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor UnsupportedColumnType = new(
            "FWP003", "Unsupported fixed-width column type",
            "Column '{0}' has type '{1}', which is not string, double, float or ISpanParsable<T>; generated parsing cannot handle it",
            "FixedWidthParser", DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor NegativeStart = new(
            "FWP004", "Invalid fixed-width column start",
            "Column '{0}' in '{1}' has a negative Start ({2})",
            "FixedWidthParser", DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor InvalidLength = new(
            "FWP005", "Invalid fixed-width column length",
            "Column '{0}' in '{1}' has an invalid Length ({2}); it must be >= 1",
            "FixedWidthParser", DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor OverlappingColumns = new(
            "FWP006", "Overlapping fixed-width columns",
            "Overlapping columns in '{0}': '{1}' [{2}, {3}) and '{4}' [{5}, {6})",
            "FixedWidthParser", DiagnosticSeverity.Error, isEnabledByDefault: true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var models = context.SyntaxProvider.CreateSyntaxProvider(
                    predicate: static (node, _) => node is TypeDeclarationSyntax { BaseList: not null },
                    transform: static (ctx, ct) => Extract(ctx, ct))
                .Where(static m => m is not null);

            context.RegisterSourceOutput(models, static (spc, model) => Emit(spc, model!));
        }
        private static ModelInfo? Extract(GeneratorSyntaxContext ctx, System.Threading.CancellationToken ct)
        {
            var declaration = (TypeDeclarationSyntax)ctx.Node;
            var symbol = ctx.SemanticModel.GetDeclaredSymbol(declaration, ct);
            if (symbol is null)
            {
                return null;
            }

            var compilation = ctx.SemanticModel.Compilation;
            var marker = compilation.GetTypeByMetadataName(MarkerMetadataName);
            var columnAttribute = compilation.GetTypeByMetadataName(ColumnAttributeMetadataName);
            if (marker is null || columnAttribute is null)
            {
                return null;
            }

            // Implements IFixedWidthModel<ThisType>?
            bool implementsMarker = symbol.AllInterfaces.Any(i =>
                SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, marker)
                && i.TypeArguments.Length == 1
                && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], symbol));
            if (!implementsMarker)
            {
                return null;
            }

            bool isPartial = declaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
            bool isNested = symbol.ContainingType is not null;

            var columns = ImmutableArray.CreateBuilder<ColumnInfo>();
            foreach (var member in symbol.GetMembers())
            {
                if (member.IsStatic)
                {
                    continue;
                }

                ITypeSymbol? memberType = member switch
                {
                    IPropertySymbol { SetMethod: not null, DeclaredAccessibility: Accessibility.Public } p => p.Type,
                    IFieldSymbol { IsReadOnly: false, IsConst: false, DeclaredAccessibility: Accessibility.Public } f => f.Type,
                    _ => null
                };
                if (memberType is null)
                {
                    continue;
                }

                var attribute = member.GetAttributes().FirstOrDefault(a =>
                    SymbolEqualityComparer.Default.Equals(a.AttributeClass, columnAttribute));
                if (attribute is null || attribute.ConstructorArguments.Length < 2)
                {
                    continue;
                }

                int start = attribute.ConstructorArguments[0].Value is int s ? s : 0;
                int length = attribute.ConstructorArguments[1].Value is int l ? l : 0;
                var (kind, parsableTypeFqn) = Classify(memberType, compilation);
                columns.Add(new ColumnInfo(member.Name, start, length, kind, parsableTypeFqn));
            }

            string? ns = symbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : symbol.ContainingNamespace.ToDisplayString();

            return new ModelInfo(
                ns,
                BuildTypeKeywords(symbol),
                symbol.Name,
                symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                new EquatableArray<ColumnInfo>(columns.ToImmutable()),
                isPartial,
                isNested,
                declaration.Identifier.GetLocation());
        }

        private static (ColumnKind Kind, string TypeFqn) Classify(ITypeSymbol type, Compilation compilation)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_String:
                    return (ColumnKind.String, "string");
                case SpecialType.System_Double:
                    return (ColumnKind.Double, "double");
                case SpecialType.System_Single:
                    return (ColumnKind.Float, "float");
            }

            string fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var spanParsable = compilation.GetTypeByMetadataName("System.ISpanParsable`1");
            bool isSpanParsable = spanParsable is not null && type.AllInterfaces.Any(i =>
                SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, spanParsable)
                && i.TypeArguments.Length == 1
                && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], type));
            return isSpanParsable ? (ColumnKind.SpanParsable, fqn) : (ColumnKind.Unsupported, fqn);
        }

        private static string BuildTypeKeywords(INamedTypeSymbol symbol)
        {
            var sb = new StringBuilder();
            if (symbol.IsReadOnly) sb.Append("readonly ");
            if (symbol.IsRefLikeType) sb.Append("ref ");
            sb.Append("partial ");
            if (symbol.IsRecord) sb.Append("record ");
            sb.Append(symbol.TypeKind == TypeKind.Struct ? "struct" : "class");
            return sb.ToString();
        }

        private static void Emit(SourceProductionContext spc, ModelInfo model)
        {
            if (model.IsNested)
            {
                spc.ReportDiagnostic(Diagnostic.Create(MustBeTopLevel, model.Location, model.TypeName));
                return;
            }
            if (!model.IsPartial)
            {
                spc.ReportDiagnostic(Diagnostic.Create(MustBePartial, model.Location, model.TypeName));
                return;
            }

            bool hasError = false;
            var columns = model.Columns.AsImmutableArray();
            foreach (var column in columns)
            {
                if (column.Kind == ColumnKind.Unsupported)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(UnsupportedColumnType, model.Location, column.Name, column.TypeFqn));
                    hasError = true;
                }
                if (column.Start < 0)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(NegativeStart, model.Location, column.Name, model.TypeName, column.Start));
                    hasError = true;
                }
                if (column.Length < 1)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(InvalidLength, model.Location, column.Name, model.TypeName, column.Length));
                    hasError = true;
                }
            }

            if (columns.Length > 1)
            {
                var sorted = columns.ToArray();
                Array.Sort(sorted, static (a, b) =>
                    a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.Length.CompareTo(b.Length));

                var farthest = sorted[0];
                int maxEnd = farthest.Start + farthest.Length;
                for (int i = 1; i < sorted.Length; i++)
                {
                    var current = sorted[i];
                    if (current.Start < maxEnd)
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            OverlappingColumns,
                            model.Location,
                            model.TypeName,
                            farthest.Name,
                            farthest.Start,
                            farthest.Start + farthest.Length,
                            current.Name,
                            current.Start,
                            current.Start + current.Length));
                        hasError = true;
                    }

                    int end = current.Start + current.Length;
                    if (end > maxEnd)
                    {
                        maxEnd = end;
                        farthest = current;
                    }
                }
            }
            if (hasError)
            {
                return;
            }

            string hint = model.FullyQualifiedName.Replace("global::", string.Empty) + ".FixedWidth.g.cs";
            spc.AddSource(hint, BuildSource(model));
        }

        private static string BuildSource(ModelInfo model)
        {
            var columns = model.Columns.AsImmutableArray();
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();

            string indent = "    ";
            if (model.Namespace is not null)
            {
                sb.Append("namespace ").Append(model.Namespace).AppendLine();
                sb.AppendLine("{");
            }
            else
            {
                indent = string.Empty;
            }

            sb.Append(indent).Append(model.TypeKeywords).Append(' ').Append(model.TypeName).AppendLine();
            sb.Append(indent).AppendLine("{");

            string body = indent + "    ";
            sb.Append(body)
              .Append("public static bool TryParse(global::System.ReadOnlySpan<char> line, global::System.IFormatProvider? formatProvider, global::CommunityToolkit.HighPerformance.Buffers.StringPool? stringPool, out ")
              .Append(model.FullyQualifiedName).AppendLine(" model)");
            sb.Append(body).AppendLine("{");

            string stmt = body + "    ";
            if (columns.Length > 0)
            {
                int maxEnd = 0;
                for (int i = 0; i < columns.Length; i++)
                {
                    int end = columns[i].Start + columns[i].Length;
                    if (end > maxEnd)
                    {
                        maxEnd = end;
                    }
                }

                sb.Append(stmt).Append("if (line.Length < ").Append(maxEnd).AppendLine(") { model = default!; return false; }");
                AppendParseAndReturn(sb, stmt, model, static c => "line.Slice(" + c.Start + ", " + c.Length + ")");
            }
            else
            {
                AppendParseAndReturn(sb, stmt, model, static _ => "default");
            }

            sb.Append(body).AppendLine("}");
            sb.Append(indent).AppendLine("}");
            if (model.Namespace is not null)
            {
                sb.AppendLine("}");
            }
            return sb.ToString();
        }

        private static void AppendParseAndReturn(
            StringBuilder sb,
            string stmt,
            ModelInfo model,
            Func<ColumnInfo, string> columnExpression)
        {
            var columns = model.Columns.AsImmutableArray();
            for (int i = 0; i < columns.Length; i++)
            {
                var c = columns[i];
                string col = columnExpression(c);
                switch (c.Kind)
                {
                    case ColumnKind.String:
                        sb.Append(stmt).Append("string __v").Append(i)
                          .Append(" = global::FixedWidthParser.FixedWidthRuntime.String(").Append(col).AppendLine(", stringPool);");
                        break;
                    case ColumnKind.Double:
                        sb.Append(stmt).Append("if (!global::FixedWidthParser.FixedWidthRuntime.TryDouble(").Append(col)
                          .Append(", formatProvider, out double __v").Append(i).AppendLine(")) { model = default!; return false; }");
                        break;
                    case ColumnKind.Float:
                        sb.Append(stmt).Append("if (!global::FixedWidthParser.FixedWidthRuntime.TryFloat(").Append(col)
                          .Append(", formatProvider, out float __v").Append(i).AppendLine(")) { model = default!; return false; }");
                        break;
                    case ColumnKind.SpanParsable:
                        sb.Append(stmt).Append("if (!global::FixedWidthParser.FixedWidthRuntime.TryParse<").Append(c.TypeFqn).Append(">(").Append(col)
                          .Append(", formatProvider, out ").Append(c.TypeFqn).Append(" __v").Append(i).AppendLine(")) { model = default!; return false; }");
                        break;
                }
            }

            sb.Append(stmt).Append("model = new ").Append(model.FullyQualifiedName).AppendLine();
            sb.Append(stmt).AppendLine("{");
            for (int i = 0; i < columns.Length; i++)
            {
                sb.Append(stmt).Append("    ").Append(columns[i].Name).Append(" = __v").Append(i).AppendLine(",");
            }
            sb.Append(stmt).AppendLine("};");
            sb.Append(stmt).AppendLine("return true;");
        }

        private enum ColumnKind { String, Double, Float, SpanParsable, Unsupported }

        private readonly struct ColumnInfo : IEquatable<ColumnInfo>
        {
            public readonly string Name;
            public readonly int Start;
            public readonly int Length;
            public readonly ColumnKind Kind;
            public readonly string TypeFqn;

            public ColumnInfo(string name, int start, int length, ColumnKind kind, string typeFqn)
            {
                Name = name;
                Start = start;
                Length = length;
                Kind = kind;
                TypeFqn = typeFqn;
            }

            public bool Equals(ColumnInfo other)
                => Start == other.Start && Length == other.Length && Kind == other.Kind
                   && Name == other.Name && TypeFqn == other.TypeFqn;

            public override bool Equals(object? obj) => obj is ColumnInfo other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + Name.GetHashCode();
                    hash = hash * 31 + Start;
                    hash = hash * 31 + Length;
                    hash = hash * 31 + (int)Kind;
                    hash = hash * 31 + TypeFqn.GetHashCode();
                    return hash;
                }
            }
        }

        private sealed class ModelInfo
        {
            public string? Namespace { get; }
            public string TypeKeywords { get; }
            public string TypeName { get; }
            public string FullyQualifiedName { get; }
            public EquatableArray<ColumnInfo> Columns { get; }
            public bool IsPartial { get; }
            public bool IsNested { get; }
            public Location Location { get; }

            public ModelInfo(string? ns, string typeKeywords, string typeName, string fullyQualifiedName,
                EquatableArray<ColumnInfo> columns, bool isPartial, bool isNested, Location location)
            {
                Namespace = ns;
                TypeKeywords = typeKeywords;
                TypeName = typeName;
                FullyQualifiedName = fullyQualifiedName;
                Columns = columns;
                IsPartial = isPartial;
                IsNested = isNested;
                Location = location;
            }
        }

        /// <summary>Value-equatable wrapper so the incremental pipeline caches correctly.</summary>
        private readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>> where T : IEquatable<T>
        {
            private readonly ImmutableArray<T> _array;

            public EquatableArray(ImmutableArray<T> array) => _array = array;

            public ImmutableArray<T> AsImmutableArray() => _array.IsDefault ? ImmutableArray<T>.Empty : _array;

            public bool Equals(EquatableArray<T> other)
            {
                var a = AsImmutableArray();
                var b = other.AsImmutableArray();
                if (a.Length != b.Length)
                {
                    return false;
                }
                for (int i = 0; i < a.Length; i++)
                {
                    if (!a[i].Equals(b[i]))
                    {
                        return false;
                    }
                }
                return true;
            }

            public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    foreach (var item in AsImmutableArray())
                    {
                        hash = hash * 31 + item.GetHashCode();
                    }
                    return hash;
                }
            }
        }
    }
}
