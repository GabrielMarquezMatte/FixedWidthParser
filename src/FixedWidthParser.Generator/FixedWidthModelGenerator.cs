using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FixedWidthParser.Generator
{
    /// <summary>
    /// Emits a reflection-free <c>TryParse</c> and <c>TryFormat</c> over bytes for every
    /// <see langword="partial"/> type that declares <c>FixedWidthParser.IFixedWidthModel&lt;TSelf&gt;</c>,
    /// mirroring the runtime parser's and writer's semantics.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class FixedWidthModelGenerator : IIncrementalGenerator
    {
        private const string MarkerMetadataName = "FixedWidthParser.IFixedWidthModel`1";
        private const string ColumnAttributeMetadataName = "FixedWidthParser.Attributes.FixedColumnAttribute";
        private const string ConverterInterfaceMetadataName = "FixedWidthParser.Processors.IFixedWidthConverter`1";
        private const string SpanFormattableMetadataName = "System.ISpanFormattable";
        private const string RuntimeFqn = "global::FixedWidthParser.FixedWidthRuntime";

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
            "Column '{0}' has type '{1}', which is not string, double, float, IUtf8SpanParsable<T> or ISpanParsable<T>; set a Converter for it",
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

        private static readonly DiagnosticDescriptor UnsupportedConverterType = new(
            "FWP008", "Converter does not implement IFixedWidthConverter<T>",
            "Converter '{0}' for column '{1}' does not implement IFixedWidthConverter<{2}>",
            "FixedWidthParser", DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor UnsupportedWriteColumnType = new(
            "FWP010", "Unsupported fixed-width column type for writing",
            "Column '{0}' has type '{1}', which is not string or ISpanFormattable; set a Converter for it",
            "FixedWidthParser", DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor InvalidOverflowBehavior = new(
            "FWP011", "Invalid overflow behavior",
            "Column '{0}' in '{1}' has type '{2}' and specifies OverflowBehavior.Truncate, which is only supported for string columns",
            "FixedWidthParser", DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor NonAsciiCharacter = new(
            "FWP012", "Padding and TrimChar must be ASCII",
            "Column '{0}' in '{1}' sets {2} to '{3}', which is not an ASCII character; column positions are bytes, so it must be one byte in every supported encoding",
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
            if (columnAttribute is null || marker is null || !ImplementsMarker(symbol, marker))
            {
                return null;
            }

            var columns = ImmutableArray.CreateBuilder<ColumnInfo>();
            foreach (var member in symbol.GetMembers())
            {
                if (ExtractColumn(member, columnAttribute, compilation) is { } column)
                {
                    columns.Add(column);
                }
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
                declaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)),
                symbol.ContainingType is not null,
                declaration.Identifier.GetLocation());
        }

        // A settable public instance property or field carrying [FixedColumn]; null for any other member.
        private static ColumnInfo? ExtractColumn(ISymbol member, INamedTypeSymbol columnAttribute, Compilation compilation)
        {
            if (member.IsStatic)
            {
                return null;
            }

            ITypeSymbol? memberType = member switch
            {
                IPropertySymbol { SetMethod: not null, DeclaredAccessibility: Accessibility.Public } p => p.Type,
                IFieldSymbol { IsReadOnly: false, IsConst: false, DeclaredAccessibility: Accessibility.Public } f => f.Type,
                _ => null
            };
            if (memberType is null)
            {
                return null;
            }

            var attribute = member.GetAttributes().FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, columnAttribute));
            if (attribute is null || attribute.ConstructorArguments.Length < 2)
            {
                return null;
            }

            int start = attribute.ConstructorArguments[0].Value is int s ? s : 0;
            int length = attribute.ConstructorArguments[1].Value is int l ? l : 0;

            ITypeSymbol? converterType = null;
            int alignment = 0; // Alignment.Left
            char padding = ' ';
            string? format = null;
            int overflow = 0; // OverflowBehavior.Default
            char trimChar = ' ';
            int trimMode = 0; // TrimMode.Trailing
            foreach (var namedArg in attribute.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "Converter" when namedArg.Value.Value is ITypeSymbol converterSymbol:
                        converterType = converterSymbol;
                        break;
                    case "Alignment" when namedArg.Value.Value is int a:
                        alignment = a;
                        break;
                    case "Padding" when namedArg.Value.Value is char p:
                        padding = p;
                        break;
                    case "Format":
                        format = namedArg.Value.Value as string;
                        break;
                    case "Overflow" when namedArg.Value.Value is int o:
                        overflow = o;
                        break;
                    case "TrimChar" when namedArg.Value.Value is char t:
                        trimChar = t;
                        break;
                    case "TrimMode" when namedArg.Value.Value is int tm:
                        trimMode = tm;
                        break;
                }
            }

            var underlying = UnwrapNullable(memberType);
            bool isNullable = !SymbolEqualityComparer.Default.Equals(underlying, memberType);
            string? converterFqn = converterType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var kind = Classify(underlying, converterType, format, compilation);
            var writeKind = ClassifyWrite(underlying, converterType, compilation);
            // Mirrors FormatterBuilder.ResolveOptions: an explicit Overflow wins; otherwise string truncates and everything else throws.
            int resolvedOverflow = overflow;
            if (resolvedOverflow == 0)
            {
                resolvedOverflow = writeKind == WriteKind.String ? 1 : 2;
            }

            return new ColumnInfo(
                member.Name, start, length, kind, underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), converterFqn,
                isNullable, writeKind, alignment, padding, format, resolvedOverflow, trimChar, trimMode);
        }

        // Implements IFixedWidthModel<ThisType>?
        private static bool ImplementsMarker(INamedTypeSymbol symbol, INamedTypeSymbol marker)
        {
            return symbol.AllInterfaces.Any(i =>
                SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, marker)
                && i.TypeArguments.Length == 1
                && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], symbol));
        }

        // Mirrors ColumnParserFactory: converter → date/time with Format → string → double/float →
        // IUtf8SpanParsable → ISpanParsable over the decoded text.
        private static ColumnKind Classify(ITypeSymbol type, ITypeSymbol? converterType, string? format, Compilation compilation)
        {
            if (converterType is not null)
            {
                return ImplementsGeneric(converterType, type, compilation, ConverterInterfaceMetadataName) ? ColumnKind.Converter : ColumnKind.BadConverter;
            }

            string fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (format is not null && ExactHelper(fqn) is not null)
            {
                return ColumnKind.Exact;
            }

            switch (type.SpecialType)
            {
                case SpecialType.System_String:
                    return ColumnKind.String;
                case SpecialType.System_Double:
                    return ColumnKind.Double;
                case SpecialType.System_Single:
                    return ColumnKind.Float;
                default:
                    break;
            }

            if (ImplementsGeneric(type, type, compilation, "System.IUtf8SpanParsable`1"))
            {
                return ColumnKind.Utf8Parsable;
            }
            return ImplementsGeneric(type, type, compilation, "System.ISpanParsable`1") ? ColumnKind.DecodedParsable : ColumnKind.Unsupported;
        }

        // Mirrors FormatterBuilder: converter → string → ISpanFormattable.
        private static WriteKind ClassifyWrite(ITypeSymbol type, ITypeSymbol? converterType, Compilation compilation)
        {
            if (converterType is not null)
            {
                return ImplementsGeneric(converterType, type, compilation, ConverterInterfaceMetadataName) ? WriteKind.Converter : WriteKind.Unsupported;
            }
            if (type.SpecialType == SpecialType.System_String)
            {
                return WriteKind.String;
            }
            if (!Implements(type, compilation, SpanFormattableMetadataName))
            {
                return WriteKind.Unsupported;
            }
            return Implements(type, compilation, "System.IUtf8SpanFormattable") ? WriteKind.Utf8Formattable : WriteKind.Formattable;
        }

        private static bool Implements(ITypeSymbol type, Compilation compilation, string interfaceMetadataName)
        {
            var iface = compilation.GetTypeByMetadataName(interfaceMetadataName);
            return iface is not null && type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iface));
        }

        private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
        {
            return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named
                ? named.TypeArguments[0]
                : type;
        }

        // Does `type` implement the open interface `interfaceMetadataName` closed over `argument`?
        private static bool ImplementsGeneric(ITypeSymbol type, ITypeSymbol argument, Compilation compilation, string interfaceMetadataName)
        {
            var iface = compilation.GetTypeByMetadataName(interfaceMetadataName);
            return iface is not null && type.AllInterfaces.Any(i =>
                SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iface)
                && i.TypeArguments.Length == 1
                && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], argument));
        }

        private static string? ExactHelper(string fqn)
        {
            return fqn switch
            {
                "global::System.DateTime" => "TryDateTimeExact",
                "global::System.DateOnly" => "TryDateOnlyExact",
                "global::System.TimeOnly" => "TryTimeOnlyExact",
                "global::System.DateTimeOffset" => "TryDateTimeOffsetExact",
                _ => null,
            };
        }

        private static string BuildTypeKeywords(INamedTypeSymbol symbol)
        {
            var sb = new StringBuilder();
            if (symbol.IsReadOnly)
            {
                sb.Append("readonly ");
            }

            if (symbol.IsRefLikeType)
            {
                sb.Append("ref ");
            }

            sb.Append("partial ");
            if (symbol.IsRecord)
            {
                sb.Append("record ");
            }

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

            // The interface needs both methods, so any column error blocks the whole emission.
            bool error = false;
            var columns = model.Columns.AsImmutableArray();
            foreach (var column in columns)
            {
                error |= ReportColumnErrors(spc, model, column);
            }
            error |= ReportOverlaps(spc, model, columns);
            if (error)
            {
                return;
            }

            spc.AddSource(model.FullyQualifiedName.Replace("global::", string.Empty) + ".FixedWidth.g.cs", BuildSource(model));
        }

        private static bool ReportColumnErrors(SourceProductionContext spc, ModelInfo model, ColumnInfo column)
        {
            bool error = false;
            void Report(DiagnosticDescriptor descriptor, params object?[] args)
            {
                spc.ReportDiagnostic(Diagnostic.Create(descriptor, model.Location, args));
                error = true;
            }

            switch (column.Kind)
            {
                case ColumnKind.BadConverter:
                    Report(UnsupportedConverterType, column.ConverterFqn, column.Name, column.TypeFqn);
                    break;
                case ColumnKind.Unsupported:
                    Report(UnsupportedColumnType, column.Name, column.TypeFqn);
                    break;
                default:
                    if (column.WKind == WriteKind.Unsupported)
                    {
                        Report(UnsupportedWriteColumnType, column.Name, column.TypeFqn);
                    }
                    break;
            }
            if (column.Overflow == 1 && column.WKind != WriteKind.String)
            {
                Report(InvalidOverflowBehavior, column.Name, model.TypeName, column.TypeFqn);
            }
            if (column.Padding > '\x7F')
            {
                Report(NonAsciiCharacter, column.Name, model.TypeName, "Padding", column.Padding);
            }
            if (column.TrimChar > '\x7F')
            {
                Report(NonAsciiCharacter, column.Name, model.TypeName, "TrimChar", column.TrimChar);
            }
            if (column.Start < 0)
            {
                Report(NegativeStart, column.Name, model.TypeName, column.Start);
            }
            if (column.Length < 1)
            {
                Report(InvalidLength, column.Name, model.TypeName, column.Length);
            }
            return error;
        }

        private static bool ReportOverlaps(SourceProductionContext spc, ModelInfo model, ImmutableArray<ColumnInfo> columns)
        {
            if (columns.Length < 2)
            {
                return false;
            }

            bool error = false;
            var sorted = columns.ToArray();
            Array.Sort(sorted, static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.Length.CompareTo(b.Length));
            var farthest = sorted[0];
            int maxEnd = farthest.Start + farthest.Length;
            foreach (ref readonly var current in sorted.AsSpan(1))
            {
                if (current.Start < maxEnd)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        OverlappingColumns, model.Location, model.TypeName,
                        farthest.Name, farthest.Start, farthest.Start + farthest.Length,
                        current.Name, current.Start, current.Start + current.Length));
                    error = true;
                }

                int end = current.Start + current.Length;
                if (end > maxEnd)
                {
                    maxEnd = end;
                    farthest = current;
                }
            }
            return error;
        }

        private static string BuildSource(ModelInfo model)
        {
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
            AppendFields(sb, body, model);
            sb.AppendLine();
            AppendParseMethod(sb, body, model);
            sb.AppendLine();
            AppendFormatMethod(sb, body, model);

            sb.Append(indent).AppendLine("}");
            if (model.Namespace is not null)
            {
                sb.AppendLine("}");
            }
            return sb.ToString();
        }

        // One converter instance per converter column (created once, shared by parse and format) and
        // one ColumnFormatOptions per column, resolved at compile time.
        private static void AppendFields(StringBuilder sb, string body, ModelInfo model)
        {
            var columns = model.Columns.AsImmutableArray();
            for (int i = 0; i < columns.Length; i++)
            {
                var c = columns[i];
                if (c.Kind == ColumnKind.Converter)
                {
                    sb.Append(body).Append("private static readonly ").Append(c.ConverterFqn)
                      .Append(" __converter").Append(i).Append(" = new ").Append(c.ConverterFqn).AppendLine("();");
                }
                sb.Append(body).Append("private static readonly global::FixedWidthParser.Formatters.ColumnFormatOptions __options").Append(i)
                  .Append(" = new(").Append(AlignmentLiteral(c.Alignment)).Append(", ").Append(SymbolDisplay.FormatLiteral(c.Padding, true))
                  .Append(", ").Append(c.Format is null ? "null" : SymbolDisplay.FormatLiteral(c.Format, true))
                  .Append(", ").Append(OverflowLiteral(c.Overflow)).AppendLine(");");
            }
        }

        private static int LineLength(ModelInfo model)
        {
            int length = 0;
            foreach (var c in model.Columns.AsImmutableArray())
            {
                length = Math.Max(length, c.Start + c.Length);
            }
            return length;
        }

        private static void AppendParseMethod(StringBuilder sb, string body, ModelInfo model)
        {
            var columns = model.Columns.AsImmutableArray();
            sb.Append(body)
              .AppendLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            sb.Append(body)
              .Append("public static bool TryParse(global::System.ReadOnlySpan<byte> line, in global::FixedWidthParser.FixedWidthContext context, out ")
              .Append(model.FullyQualifiedName).AppendLine(" model)");
            sb.Append(body).AppendLine("{");

            string stmt = body + "    ";
            int lineLength = LineLength(model);
            if (lineLength > 0)
            {
                sb.Append(stmt).Append("if (line.Length < ").Append(lineLength).Append(") { context.ReportShortLine(").Append(lineLength)
                  .AppendLine("); model = default!; return false; }");
            }

            for (int i = 0; i < columns.Length; i++)
            {
                var c = columns[i];
                string col = "line.Slice(" + c.Start + ", " + c.Length + ")";
                if (!c.IsNullable)
                {
                    AppendColumnParse(sb, stmt, c, col, i, "__v" + i);
                    continue;
                }

                // T?: a blank column assigns null without invoking the underlying parser.
                sb.Append(stmt).Append(c.TypeFqn).Append("? __v").Append(i).AppendLine(";");
                sb.Append(stmt).Append("if (").Append(RuntimeFqn).Append(".IsBlank(").Append(col).Append(", ").Append(ByteLiteral(c.TrimChar)).AppendLine("))");
                sb.Append(stmt).AppendLine("{");
                sb.Append(stmt).Append("    __v").Append(i).AppendLine(" = null;");
                sb.Append(stmt).AppendLine("}");
                sb.Append(stmt).AppendLine("else");
                sb.Append(stmt).AppendLine("{");
                AppendColumnParse(sb, stmt + "    ", c, col, i, "__u" + i);
                sb.Append(stmt).Append("    __v").Append(i).Append(" = __u").Append(i).AppendLine(";");
                sb.Append(stmt).AppendLine("}");
            }

            sb.Append(stmt).Append("model = new ").Append(model.FullyQualifiedName).AppendLine();
            sb.Append(stmt).AppendLine("{");
            for (int i = 0; i < columns.Length; i++)
            {
                sb.Append(stmt).Append("    ").Append(columns[i].Name).Append(" = __v").Append(i).AppendLine(",");
            }
            sb.Append(stmt).AppendLine("};");
            sb.Append(stmt).AppendLine("return true;");
            sb.Append(body).AppendLine("}");
        }

        // Emits the kind-specific parse statement for one column into a local named localName.
        private static void AppendColumnParse(StringBuilder sb, string stmt, ColumnInfo c, string col, int i, string localName)
        {
            string trimArg = TrimArgSuffix(c);
            string fail = ")) { context.ReportFailedColumn(" + SymbolDisplay.FormatLiteral(c.Name, true) + ", " + c.Start + ", " + c.Length
                          + "); model = default!; return false; }";

            switch (c.Kind)
            {
                case ColumnKind.Exact:
                    sb.Append(stmt).Append("if (!").Append(RuntimeFqn).Append('.').Append(ExactHelper(c.TypeFqn)).Append('(').Append(col)
                      .Append(", ").Append(SymbolDisplay.FormatLiteral(c.Format!, true)).Append(", context, out var ").Append(localName).Append(trimArg).AppendLine(fail);
                    break;
                case ColumnKind.String:
                    sb.Append(stmt).Append("string ").Append(localName)
                      .Append(" = ").Append(RuntimeFqn).Append(".String(").Append(col).Append(", context").Append(trimArg).AppendLine(");");
                    break;
                case ColumnKind.Double:
                    sb.Append(stmt).Append("if (!").Append(RuntimeFqn).Append(".TryDouble(").Append(col)
                      .Append(", context, out double ").Append(localName).Append(trimArg).AppendLine(fail);
                    break;
                case ColumnKind.Float:
                    sb.Append(stmt).Append("if (!").Append(RuntimeFqn).Append(".TryFloat(").Append(col)
                      .Append(", context, out float ").Append(localName).Append(trimArg).AppendLine(fail);
                    break;
                case ColumnKind.Utf8Parsable:
                case ColumnKind.DecodedParsable:
                    sb.Append(stmt).Append("if (!").Append(RuntimeFqn).Append(c.Kind == ColumnKind.Utf8Parsable ? ".TryParse<" : ".TryParseDecoded<")
                      .Append(c.TypeFqn).Append(">(").Append(col).Append(", context, out ").Append(c.TypeFqn).Append(' ').Append(localName).Append(trimArg).AppendLine(fail);
                    break;
                case ColumnKind.Converter:
                    sb.Append(stmt).Append("if (!").Append(RuntimeFqn).Append(".TryConvert<").Append(c.TypeFqn).Append(", ").Append(c.ConverterFqn)
                      .Append(">(").Append(col).Append(", context, __converter").Append(i).Append(", out ").Append(c.TypeFqn)
                      .Append(' ').Append(localName).Append(trimArg).AppendLine(fail);
                    break;
                default:
                    // Unsupported kinds were reported in Emit, which then skips generation.
                    break;
            }
        }

        private static void AppendFormatMethod(StringBuilder sb, string body, ModelInfo model)
        {
            var columns = model.Columns.AsImmutableArray();
            int lineLength = LineLength(model);
            sb.Append(body)
              .AppendLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            sb.Append(body)
              .Append("public static bool TryFormat(in ").Append(model.FullyQualifiedName)
              .AppendLine(" model, global::System.Span<byte> destination, in global::FixedWidthParser.FixedWidthContext context, out int bytesWritten)");
            sb.Append(body).AppendLine("{");

            string stmt = body + "    ";
            sb.Append(stmt).Append("if (destination.Length < ").Append(lineLength).AppendLine(") { bytesWritten = 0; return false; }");
            if (columns.Length > 0)
            {
                sb.Append(stmt).Append("var __line = destination[..").Append(lineLength).AppendLine("];");
                sb.Append(stmt).AppendLine("__line.Fill((byte)' ');");
                for (int i = 0; i < columns.Length; i++)
                {
                    AppendColumnFormat(sb, stmt, columns[i], i);
                }
            }
            sb.Append(stmt).Append("bytesWritten = ").Append(lineLength).AppendLine(";");
            sb.Append(stmt).AppendLine("return true;");
            sb.Append(body).AppendLine("}");
        }

        // A nullable (T?) column fills its slice with its trim character when null.
        private static void AppendColumnFormat(StringBuilder sb, string stmt, ColumnInfo c, int i)
        {
            string slice = "__line.Slice(" + c.Start + ", " + c.Length + ")";
            if (!c.IsNullable)
            {
                AppendColumnKindFormat(sb, stmt, c, slice, i, "model." + c.Name);
                return;
            }

            sb.Append(stmt).Append("if (model.").Append(c.Name).Append(" is { } __u").Append(i).AppendLine(")");
            sb.Append(stmt).AppendLine("{");
            AppendColumnKindFormat(sb, stmt + "    ", c, slice, i, "__u" + i);
            sb.Append(stmt).AppendLine("}");
            sb.Append(stmt).AppendLine("else");
            sb.Append(stmt).AppendLine("{");
            sb.Append(stmt).Append("    ").Append(slice).Append(".Fill(").Append(ByteLiteral(c.TrimChar)).AppendLine(");");
            sb.Append(stmt).AppendLine("}");
        }

        private static void AppendColumnKindFormat(StringBuilder sb, string stmt, ColumnInfo c, string slice, int i, string valueExpr)
        {
            string tail = ", __options" + i + ", " + SymbolDisplay.FormatLiteral(c.Name, true) + ");";
            switch (c.WKind)
            {
                case WriteKind.String:
                    sb.Append(stmt).Append(RuntimeFqn).Append(".FormatString(").Append(valueExpr).Append(", ").Append(slice).Append(", context").AppendLine(tail);
                    break;
                case WriteKind.Formattable:
                    sb.Append(stmt).Append(RuntimeFqn).Append(".FormatValue(").Append(valueExpr).Append(", ").Append(slice).Append(", context").AppendLine(tail);
                    break;
                case WriteKind.Utf8Formattable:
                    sb.Append(stmt).Append(RuntimeFqn).Append(".FormatUtf8Value(").Append(valueExpr).Append(", ").Append(slice).Append(", context").AppendLine(tail);
                    break;
                case WriteKind.Converter:
                    sb.Append(stmt).Append(RuntimeFqn).Append(".FormatConvert(").Append(valueExpr).Append(", ").Append(slice)
                      .Append(", context, __converter").Append(i).AppendLine(tail);
                    break;
                default:
                    // Unsupported kinds were reported in Emit, which then skips generation.
                    break;
            }
        }

        // Trailing trim arguments for a runtime parse call, omitted for the default (space, trailing).
        private static string TrimArgSuffix(ColumnInfo c)
        {
            if (c.TrimChar == ' ' && c.TrimMode == 0)
            {
                return string.Empty;
            }
            return ", " + ByteLiteral(c.TrimChar) + ", " + TrimModeLiteral(c.TrimMode);
        }

        private static string ByteLiteral(char value)
        {
            return "(byte)" + SymbolDisplay.FormatLiteral(value, true);
        }

        private static string AlignmentLiteral(int value)
        {
            return value == 1
                ? "global::FixedWidthParser.Attributes.Alignment.Right"
                : "global::FixedWidthParser.Attributes.Alignment.Left";
        }

        private static string OverflowLiteral(int resolvedValue)
        {
            return resolvedValue switch
            {
                1 => "global::FixedWidthParser.Attributes.OverflowBehavior.Truncate",
                2 => "global::FixedWidthParser.Attributes.OverflowBehavior.Throw",
                _ => "global::FixedWidthParser.Attributes.OverflowBehavior.Default",
            };
        }

        private static string TrimModeLiteral(int value)
        {
            return value switch
            {
                1 => "global::FixedWidthParser.Attributes.TrimMode.Leading",
                2 => "global::FixedWidthParser.Attributes.TrimMode.Both",
                _ => "global::FixedWidthParser.Attributes.TrimMode.Trailing"
            };
        }

        private enum ColumnKind { String, Double, Float, Utf8Parsable, DecodedParsable, Exact, Converter, BadConverter, Unsupported }

        private enum WriteKind { String, Formattable, Utf8Formattable, Converter, Unsupported }

        private readonly struct ColumnInfo(
            string name, int start, int length, ColumnKind kind, string typeFqn, string? converterFqn, bool isNullable,
            WriteKind writeKind, int alignment, char padding, string? format, int overflow, char trimChar, int trimMode) : IEquatable<ColumnInfo>
        {
            public readonly string Name = name;
            public readonly int Start = start;
            public readonly int Length = length;
            public readonly ColumnKind Kind = kind;
            public readonly string TypeFqn = typeFqn;
            public readonly string? ConverterFqn = converterFqn;
            public readonly bool IsNullable = isNullable;
            public readonly WriteKind WKind = writeKind;
            public readonly int Alignment = alignment;
            public readonly char Padding = padding;
            public readonly string? Format = format;
            public readonly int Overflow = overflow;
            public readonly char TrimChar = trimChar;
            public readonly int TrimMode = trimMode;

            public bool Equals(ColumnInfo other)
            {
                return Start == other.Start
                       && Length == other.Length
                       && Kind == other.Kind
                       && IsNullable == other.IsNullable
                       && WKind == other.WKind
                       && Alignment == other.Alignment
                       && Padding == other.Padding
                       && Overflow == other.Overflow
                       && TrimChar == other.TrimChar
                       && TrimMode == other.TrimMode
                       && string.Equals(Format, other.Format, StringComparison.Ordinal)
                       && string.Equals(Name, other.Name, StringComparison.Ordinal)
                       && string.Equals(TypeFqn, other.TypeFqn, StringComparison.Ordinal)
                       && string.Equals(ConverterFqn, other.ConverterFqn, StringComparison.Ordinal);
            }

            [ExcludeFromCodeCoverage]
            public override bool Equals(object? obj)
            {
                return obj is ColumnInfo other && Equals(other);
            }

            [ExcludeFromCodeCoverage]
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(Name);
                    hash = hash * 31 + Start;
                    hash = hash * 31 + Length;
                    hash = hash * 31 + (int)Kind;
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(TypeFqn);
                    hash = hash * 31 + (ConverterFqn is null ? 0 : StringComparer.Ordinal.GetHashCode(ConverterFqn));
                    hash = hash * 31 + (IsNullable ? 1 : 0);
                    hash = hash * 31 + (int)WKind;
                    hash = hash * 31 + Alignment;
                    hash = hash * 31 + Padding;
                    hash = hash * 31 + Overflow;
                    hash = hash * 31 + TrimChar;
                    hash = hash * 31 + TrimMode;
                    hash = hash * 31 + (Format is null ? 0 : StringComparer.Ordinal.GetHashCode(Format));
                    return hash;
                }
            }
        }

        private sealed class ModelInfo(string? ns, string typeKeywords, string typeName, string fullyQualifiedName,
                                       EquatableArray<ColumnInfo> columns, bool isPartial, bool isNested, Location location) : IEquatable<ModelInfo>
        {
            public string? Namespace { get; } = ns;
            public string TypeKeywords { get; } = typeKeywords;
            public string TypeName { get; } = typeName;
            public string FullyQualifiedName { get; } = fullyQualifiedName;
            public EquatableArray<ColumnInfo> Columns { get; } = columns;
            public bool IsPartial { get; } = isPartial;
            public bool IsNested { get; } = isNested;
            public Location Location { get; } = location;

            // Value equality (excluding Location, which doesn't affect the emitted source) so the
            // incremental pipeline caches correctly.
            [ExcludeFromCodeCoverage]
            public bool Equals(ModelInfo? other)
            {
                return other is not null
                       && IsPartial == other.IsPartial
                       && IsNested == other.IsNested
                       && string.Equals(Namespace, other.Namespace, StringComparison.Ordinal)
                       && string.Equals(TypeKeywords, other.TypeKeywords, StringComparison.Ordinal)
                       && string.Equals(TypeName, other.TypeName, StringComparison.Ordinal)
                       && string.Equals(FullyQualifiedName, other.FullyQualifiedName, StringComparison.Ordinal)
                       && Columns.Equals(other.Columns);
            }

            [ExcludeFromCodeCoverage]
            public override bool Equals(object? obj)
            {
                return Equals(obj as ModelInfo);
            }

            [ExcludeFromCodeCoverage]
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + (Namespace is null ? 0 : StringComparer.Ordinal.GetHashCode(Namespace));
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(TypeKeywords);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(TypeName);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(FullyQualifiedName);
                    hash = hash * 31 + Columns.GetHashCode();
                    hash = hash * 31 + (IsPartial ? 1 : 0);
                    hash = hash * 31 + (IsNested ? 1 : 0);
                    return hash;
                }
            }
        }

        /// <summary>Value-equatable wrapper so the incremental pipeline caches correctly.</summary>
        private readonly struct EquatableArray<T>(ImmutableArray<T> array) : IEquatable<EquatableArray<T>> where T : IEquatable<T>
        {
            public ImmutableArray<T> AsImmutableArray()
            {
                return array.IsDefault ? ImmutableArray<T>.Empty : array;
            }

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

            [ExcludeFromCodeCoverage]
            public override bool Equals(object? obj)
            {
                return obj is EquatableArray<T> other && Equals(other);
            }

            [ExcludeFromCodeCoverage]
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
