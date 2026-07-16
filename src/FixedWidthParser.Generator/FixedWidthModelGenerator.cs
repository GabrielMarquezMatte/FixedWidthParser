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
    /// Emits a reflection-free <c>TryParse</c> for every <see langword="partial"/> type that declares
    /// <c>FixedWidthParser.IFixedWidthModel&lt;TSelf&gt;</c>, mirroring the runtime parser's semantics.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class FixedWidthModelGenerator : IIncrementalGenerator
    {
        private const string MarkerMetadataName = "FixedWidthParser.IFixedWidthModel`1";
        private const string Utf8MarkerMetadataName = "FixedWidthParser.IUtf8FixedWidthModel`1";
        private const string ColumnAttributeMetadataName = "FixedWidthParser.Attributes.FixedColumnAttribute";
        private const string ConverterInterfaceMetadataName = "FixedWidthParser.Processors.IFixedWidthConverter`1";
        private const string Utf8ConverterInterfaceMetadataName = "FixedWidthParser.Processors.IUtf8FixedWidthConverter`1";
        private const string SpanFormattableMetadataName = "System.ISpanFormattable";

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

        private static readonly DiagnosticDescriptor UnsupportedUtf8ColumnType = new(
            "FWP007", "Unsupported UTF-8 fixed-width column type",
            "Column '{0}' has type '{1}', which is not string, double, float or IUtf8SpanParsable<T>; generated UTF-8 parsing cannot handle it",
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

        private static readonly DiagnosticDescriptor UnsupportedUtf8ConverterType = new(
            "FWP009", "Converter does not implement IUtf8FixedWidthConverter<T>",
            "Converter '{0}' for column '{1}' does not implement IUtf8FixedWidthConverter<{2}>",
            "FixedWidthParser", DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor UnsupportedWriteColumnType = new(
            "FWP010", "Unsupported fixed-width column type for writing",
            "Column '{0}' has type '{1}', which is not string or ISpanFormattable; generated writing cannot handle it",
            "FixedWidthParser", DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor InvalidOverflowBehavior = new(
            "FWP011", "Invalid overflow behavior",
            "Column '{0}' in '{1}' has type '{2}' and specifies OverflowBehavior.Truncate, which is only supported for string columns",
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
            var utf8Marker = compilation.GetTypeByMetadataName(Utf8MarkerMetadataName);
            var columnAttribute = compilation.GetTypeByMetadataName(ColumnAttributeMetadataName);
            if (columnAttribute is null || (marker is null && utf8Marker is null))
            {
                return null;
            }

            // Implements IFixedWidthModel<ThisType> (char) and/or IUtf8FixedWidthModel<ThisType> (byte)?
            bool implementsChar = ImplementsMarker(symbol, marker);
            bool implementsUtf8 = ImplementsMarker(symbol, utf8Marker);
            if (!implementsChar && !implementsUtf8)
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

                var (kind, parsableTypeFqn, converterFqn, isNullable, isCharParsable, isUtf8Parsable) = Classify(memberType, converterType, format, compilation);
                var (writeKind, isCharFormattable) = ClassifyWrite(memberType, converterType, compilation);
                // Mirrors FixedWidthWriter.DetermineOverflowBehavior: an explicit Overflow wins; otherwise
                // string truncates and everything else throws.
                int resolvedOverflow = overflow != 0 ? overflow : (writeKind == WriteKind.String ? 1 : 2);

                columns.Add(new ColumnInfo(
                    member.Name, start, length, kind, parsableTypeFqn, converterFqn, isNullable, isCharParsable, isUtf8Parsable,
                    writeKind, isCharFormattable, alignment, padding, format, resolvedOverflow, trimChar, trimMode));
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
                implementsChar,
                implementsUtf8,
                declaration.Identifier.GetLocation());
        }

        // Implements I…Model<ThisType> for the given (open) marker interface?
        private static bool ImplementsMarker(INamedTypeSymbol symbol, INamedTypeSymbol? marker)
        {
            return marker is not null && symbol.AllInterfaces.Any(i =>
                SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, marker)
                && i.TypeArguments.Length == 1
                && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], symbol));
        }

        private static (ColumnKind Kind, string TypeFqn, string? ConverterFqn, bool IsNullable, bool IsCharParsable, bool IsUtf8Parsable) Classify(
            ITypeSymbol type, ITypeSymbol? converterType, string? format, Compilation compilation)
        {
            // T? (Nullable<T>): classify the underlying T (including any converter, which targets T,
            // not T?) and flag IsNullable so Emit/codegen wrap it with the blank-is-null check.
            if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named)
            {
                var underlying = Classify(named.TypeArguments[0], converterType, format, compilation);
                return (underlying.Kind, underlying.TypeFqn, underlying.ConverterFqn, true, underlying.IsCharParsable, underlying.IsUtf8Parsable);
            }

            string fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // FixedColumnAttribute.Converter takes priority over the built-in primitive/parsable
            // handling, for both string and non-string column types.
            if (converterType is not null)
            {
                string converterFqn = converterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                bool convertsChar = ImplementsConverter(converterType, type, compilation, ConverterInterfaceMetadataName);
                bool convertsUtf8 = ImplementsConverter(converterType, type, compilation, Utf8ConverterInterfaceMetadataName);
                var convKind = convertsChar || convertsUtf8 ? ColumnKind.Converter : ColumnKind.Unsupported;
                return (convKind, fqn, converterFqn, false, convertsChar, convertsUtf8);
            }

            switch (type.SpecialType)
            {
                case SpecialType.System_String:
                    return (ColumnKind.String, "string", null, false, true, true);
                case SpecialType.System_Double:
                    return (ColumnKind.Double, "double", null, false, true, true);
                case SpecialType.System_Single:
                    return (ColumnKind.Float, "float", null, false, true, true);
                default:
                    break;
            }

            bool isCharParsable = ImplementsParsable(type, compilation, "System.ISpanParsable`1");
            bool isUtf8Parsable = ImplementsParsable(type, compilation, "System.IUtf8SpanParsable`1");

            bool isDateTimeType = fqn is "global::System.DateTime" or "System.DateTime" or "global::System.DateOnly" or "System.DateOnly" or "global::System.TimeOnly" or "System.TimeOnly" or "global::System.DateTimeOffset" or "System.DateTimeOffset";
            if (isDateTimeType && format is not null)
            {
                isCharParsable = true;
                isUtf8Parsable = true;
            }

            // SpanParsable when at least one path can handle it; otherwise unsupported by every path.
            var kind = isCharParsable || isUtf8Parsable ? ColumnKind.SpanParsable : ColumnKind.Unsupported;
            return (kind, fqn, null, false, isCharParsable, isUtf8Parsable);
        }

        // Classification for the write side: mirrors FixedWidthWriter.CreateFormatter's own resolution
        // (converter → string → ISpanFormattable fallback), but write has no "primitive" concept —
        // double/float are ISpanFormattable like everything else, no special-casing.
        private static (WriteKind Kind, bool IsCharFormattable) ClassifyWrite(ITypeSymbol type, ITypeSymbol? converterType, Compilation compilation)
        {
            var underlying = UnwrapNullable(type);

            if (converterType is not null)
            {
                bool convertsChar = ImplementsConverter(converterType, underlying, compilation, ConverterInterfaceMetadataName);
                bool convertsUtf8 = ImplementsConverter(converterType, underlying, compilation, Utf8ConverterInterfaceMetadataName);
                return (convertsChar || convertsUtf8 ? WriteKind.Converter : WriteKind.Unsupported, false);
            }

            if (underlying.SpecialType == SpecialType.System_String)
            {
                return (WriteKind.String, true);
            }

            bool isCharFormattable = ImplementsInterface(underlying, compilation, SpanFormattableMetadataName);
            return (isCharFormattable ? WriteKind.Formattable : WriteKind.Unsupported, isCharFormattable);
        }

        private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
        {
            return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named
                ? named.TypeArguments[0]
                : type;
        }

        // Implements the given (non-generic) BCL interface metadata name?
        private static bool ImplementsInterface(ITypeSymbol type, Compilation compilation, string interfaceMetadataName)
        {
            var iface = compilation.GetTypeByMetadataName(interfaceMetadataName);
            return iface is not null && type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iface));
        }

        // Implements I…SpanParsable<ThisType> for the given (open) BCL interface metadata name?
        private static bool ImplementsParsable(ITypeSymbol type, Compilation compilation, string interfaceMetadataName)
        {
            var parsable = compilation.GetTypeByMetadataName(interfaceMetadataName);
            return parsable is not null && type.AllInterfaces.Any(i =>
                SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, parsable)
                && i.TypeArguments.Length == 1
                && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], type));
        }

        // Implements I…Converter<columnType> (the given open interface) for the column's converter type?
        private static bool ImplementsConverter(ITypeSymbol converterType, ITypeSymbol columnType, Compilation compilation, string interfaceMetadataName)
        {
            var iface = compilation.GetTypeByMetadataName(interfaceMetadataName);
            return iface is not null && converterType.AllInterfaces.Any(i =>
                SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iface)
                && i.TypeArguments.Length == 1
                && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], columnType));
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

            // Structural errors (geometry) block ALL generated methods (parse char/byte + write).
            // Per-column-type errors block only the affected path: FWP003 the char parse method,
            // FWP007 the byte parse method, FWP010/FWP008 the write method.
            bool structuralError = false;
            bool charTypeError = false;
            bool utf8TypeError = false;
            bool writeTypeError = false;
            var columns = model.Columns.AsImmutableArray();
            foreach (var column in columns)
            {
                bool isPrimitive = column.Kind is ColumnKind.String or ColumnKind.Double or ColumnKind.Float;
                if (!isPrimitive)
                {
                    if (model.ImplementsChar && !column.IsCharParsable)
                    {
                        spc.ReportDiagnostic(column.ConverterFqn is not null
                            ? Diagnostic.Create(UnsupportedConverterType, model.Location, column.ConverterFqn, column.Name, column.TypeFqn)
                            : Diagnostic.Create(UnsupportedColumnType, model.Location, column.Name, column.TypeFqn));
                        charTypeError = true;
                    }
                    if (model.ImplementsUtf8 && !column.IsUtf8Parsable)
                    {
                        spc.ReportDiagnostic(column.ConverterFqn is not null
                            ? Diagnostic.Create(UnsupportedUtf8ConverterType, model.Location, column.ConverterFqn, column.Name, column.TypeFqn)
                            : Diagnostic.Create(UnsupportedUtf8ColumnType, model.Location, column.Name, column.TypeFqn));
                        utf8TypeError = true;
                    }
                }
                if (model.ImplementsChar)
                {
                    bool writeOk = column.WKind switch
                    {
                        WriteKind.String => true,
                        WriteKind.Converter => column.IsCharParsable, // IFixedWidthConverter<T> is symmetric read/write
                        WriteKind.Formattable => column.IsCharFormattable,
                        _ => false
                    };
                    if (!writeOk)
                    {
                        spc.ReportDiagnostic(column.ConverterFqn is not null
                            ? Diagnostic.Create(UnsupportedConverterType, model.Location, column.ConverterFqn, column.Name, column.TypeFqn)
                            : Diagnostic.Create(UnsupportedWriteColumnType, model.Location, column.Name, column.TypeFqn));
                        writeTypeError = true;
                    }
                    if (column.Overflow == 1 && column.WKind != WriteKind.String)
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(InvalidOverflowBehavior, model.Location, column.Name, model.TypeName, column.TypeFqn));
                        writeTypeError = true;
                    }
                }
                if (column.Start < 0)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(NegativeStart, model.Location, column.Name, model.TypeName, column.Start));
                    structuralError = true;
                }
                if (column.Length < 1)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(InvalidLength, model.Location, column.Name, model.TypeName, column.Length));
                    structuralError = true;
                }
            }

            if (columns.Length > 1)
            {
                var sorted = columns.ToArray();
                Array.Sort(sorted, static (a, b) =>
                    a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.Length.CompareTo(b.Length));

                var farthest = sorted[0];
                int maxEnd = farthest.Start + farthest.Length;
                foreach (ref readonly var current in sorted.AsSpan(1))
                {
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
                        structuralError = true;
                    }

                    int end = current.Start + current.Length;
                    if (end > maxEnd)
                    {
                        maxEnd = end;
                        farthest = current;
                    }
                }
            }
            if (structuralError)
            {
                return;
            }

            bool emitChar = model.ImplementsChar && !charTypeError;
            bool emitUtf8 = model.ImplementsUtf8 && !utf8TypeError;
            bool emitWrite = model.ImplementsChar && !writeTypeError;
            if (!emitChar && !emitUtf8 && !emitWrite)
            {
                return;
            }

            string hint = model.FullyQualifiedName.Replace("global::", string.Empty) + ".FixedWidth.g.cs";
            spc.AddSource(hint, BuildSource(model, emitChar, emitUtf8, emitWrite));
        }

        private const string CharRuntimeFqn = "global::FixedWidthParser.FixedWidthRuntime";
        private const string Utf8RuntimeFqn = "global::FixedWidthParser.Utf8FixedWidthRuntime";

        private static string BuildSource(ModelInfo model, bool emitChar, bool emitUtf8, bool emitWrite)
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
            bool wroteConverterFields = AppendConverterFields(sb, body, model);
            bool wroteOptionsFields = emitWrite && AppendWriteOptionsFields(sb, body, model);
            bool wroteTrimFields = emitUtf8 && AppendUtf8TrimFields(sb, body, model);
            if (wroteConverterFields || wroteOptionsFields || wroteTrimFields)
            {
                sb.AppendLine();
            }

            // TryParse (char / byte) and TryFormat (char) coexist in one partial; each static abstract
            // interface member binds implicitly to its matching span element type.
            bool wroteMethod = false;
            if (emitChar)
            {
                AppendMethod(sb, body, model, "char", CharRuntimeFqn);
                wroteMethod = true;
            }
            if (emitUtf8)
            {
                if (wroteMethod)
                {
                    sb.AppendLine();
                }
                AppendMethod(sb, body, model, "byte", Utf8RuntimeFqn);
                wroteMethod = true;
            }
            if (emitWrite)
            {
                if (wroteMethod)
                {
                    sb.AppendLine();
                }
                AppendWriteMethod(sb, body, model);
            }

            sb.Append(indent).AppendLine("}");
            if (model.Namespace is not null)
            {
                sb.AppendLine("}");
            }
            return sb.ToString();
        }

        // Emits one static readonly field per converter-backed column, so the converter instance is
        // created once (not per parse call) and shared across both TryParse overloads. Returns whether
        // any field was written.
        private static bool AppendConverterFields(StringBuilder sb, string body, ModelInfo model)
        {
            var columns = model.Columns.AsImmutableArray();
            bool any = false;
            for (int i = 0; i < columns.Length; i++)
            {
                if (columns[i].Kind != ColumnKind.Converter)
                {
                    continue;
                }
                sb.Append(body).Append("private static readonly ").Append(columns[i].ConverterFqn)
                  .Append(" __converter").Append(i).Append(" = new ").Append(columns[i].ConverterFqn).AppendLine("();");
                any = true;
            }
            return any;
        }

        // Emits one static readonly byte field per column whose TrimChar isn't the default space,
        // converting the char to its single-byte ASCII representation once (at type-init) instead of
        // on every parse call. A non-ASCII TrimChar throws there with a clear message, mirroring how
        // CultureHelpers rejects a non-ASCII decimal separator on the byte path.
        private static bool AppendUtf8TrimFields(StringBuilder sb, string body, ModelInfo model)
        {
            var columns = model.Columns.AsImmutableArray();
            bool any = false;
            for (int i = 0; i < columns.Length; i++)
            {
                if (columns[i].TrimChar == ' ')
                {
                    continue;
                }
                sb.Append(body).Append("private static readonly byte __trim").Append(i)
                  .Append(" = global::FixedWidthParser.Utf8FixedWidthRuntime.ToAsciiByte(")
                  .Append(SymbolDisplay.FormatLiteral(columns[i].TrimChar, true)).Append(", ")
                  .Append(SymbolDisplay.FormatLiteral(columns[i].Name, true)).AppendLine(");");
                any = true;
            }
            return any;
        }

        // Emits one static readonly ColumnFormatOptions field per column (alignment/padding/format/
        // overflow, resolved at Extract time), shared by the write method's per-column formatting calls.
        private static bool AppendWriteOptionsFields(StringBuilder sb, string body, ModelInfo model)
        {
            var columns = model.Columns.AsImmutableArray();
            for (int i = 0; i < columns.Length; i++)
            {
                var c = columns[i];
                sb.Append(body).Append("private static readonly global::FixedWidthParser.Formatters.ColumnFormatOptions __options").Append(i)
                  .Append(" = new(").Append(AlignmentLiteral(c.Alignment)).Append(", ").Append(SymbolDisplay.FormatLiteral(c.Padding, true))
                  .Append(", ").Append(c.Format is null ? "null" : SymbolDisplay.FormatLiteral(c.Format, true))
                  .Append(", ").Append(OverflowLiteral(c.Overflow)).AppendLine(");");
            }
            return columns.Length > 0;
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

        private static void AppendWriteMethod(StringBuilder sb, string body, ModelInfo model)
        {
            var columns = model.Columns.AsImmutableArray();
            int lineLength = 0;
            foreach (var c in columns)
            {
                int end = c.Start + c.Length;
                if (end > lineLength)
                {
                    lineLength = end;
                }
            }

            sb.Append(body)
              .AppendLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            sb.Append(body)
              .Append("public static bool TryFormat(in ").Append(model.FullyQualifiedName)
              .Append(" model, global::System.Span<char> destination, global::System.IFormatProvider? formatProvider, out int charsWritten)").AppendLine();
            sb.Append(body).AppendLine("{");

            string stmt = body + "    ";
            sb.Append(stmt).Append("if (destination.Length < ").Append(lineLength).AppendLine(") { charsWritten = 0; return false; }");
            if (columns.Length > 0)
            {
                sb.Append(stmt).Append("var __line = destination[..").Append(lineLength).AppendLine("];");
                sb.Append(stmt).AppendLine("__line.Fill(' ');");
                for (int i = 0; i < columns.Length; i++)
                {
                    var c = columns[i];
                    string slice = "__line.Slice(" + c.Start + ", " + c.Length + ")";
                    AppendColumnWrite(sb, stmt, c, slice, i);
                }
            }
            sb.Append(stmt).Append("charsWritten = ").Append(lineLength).AppendLine(";");
            sb.Append(stmt).AppendLine("return true;");

            sb.Append(body).AppendLine("}");
        }

        // Writes one column: a nullable (T?) column fills its slice with the padding character when
        // null, otherwise formats exactly as the non-nullable case would (via the temp local pattern
        // variable from the `is { }` check).
        private static void AppendColumnWrite(StringBuilder sb, string stmt, ColumnInfo c, string slice, int i)
        {
            if (!c.IsNullable)
            {
                AppendColumnKindWrite(sb, stmt, c, slice, i, "model." + c.Name);
                return;
            }

            sb.Append(stmt).Append("if (model.").Append(c.Name).Append(" is { } __u").Append(i).AppendLine(")");
            sb.Append(stmt).AppendLine("{");
            AppendColumnKindWrite(sb, stmt + "    ", c, slice, i, "__u" + i);
            sb.Append(stmt).AppendLine("}");
            sb.Append(stmt).AppendLine("else");
            sb.Append(stmt).AppendLine("{");
            char fillChar = c.TrimChar != ' ' ? c.TrimChar : ' ';
            sb.Append(stmt).Append("    ").Append(slice).Append(".Fill(").Append(SymbolDisplay.FormatLiteral(fillChar, true)).AppendLine(");");
            sb.Append(stmt).AppendLine("}");
        }

        private static void AppendColumnKindWrite(StringBuilder sb, string stmt, ColumnInfo c, string slice, int i, string valueExpr)
        {
            switch (c.WKind)
            {
                case WriteKind.String:
                    sb.Append(stmt).Append("global::FixedWidthParser.FixedWidthRuntime.FormatString(").Append(valueExpr).Append(", ").Append(slice)
                      .Append(", __options").Append(i).Append(", \"").Append(c.Name).AppendLine("\");");
                    break;
                case WriteKind.Formattable:
                    sb.Append(stmt).Append("global::FixedWidthParser.FixedWidthRuntime.FormatValue(").Append(valueExpr).Append(", ").Append(slice)
                      .Append(", formatProvider, __options").Append(i).Append(", \"").Append(c.Name).AppendLine("\");");
                    break;
                case WriteKind.Converter:
                    sb.Append(stmt).Append("global::FixedWidthParser.FixedWidthRuntime.FormatConvert(").Append(valueExpr).Append(", ").Append(slice)
                      .Append(", formatProvider, __converter").Append(i).Append(", __options").Append(i).Append(", \"").Append(c.Name).AppendLine("\");");
                    break;
                default:
                    // Should not happen, already filtered in Emit with a diagnostic.
                    break;
            }
        }

        private static void AppendMethod(StringBuilder sb, string body, ModelInfo model, string elementType, string runtimeFqn)
        {
            var columns = model.Columns.AsImmutableArray();
            sb.Append(body)
              .AppendLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            sb.Append(body)
              .Append("public static bool TryParse(global::System.ReadOnlySpan<").Append(elementType)
              .Append("> line, global::System.IFormatProvider? formatProvider, global::CommunityToolkit.HighPerformance.Buffers.StringPool? stringPool, out ")
              .Append(model.FullyQualifiedName).AppendLine(" model)");
            sb.Append(body).AppendLine("{");

            string stmt = body + "    ";
            if (columns.Length > 0)
            {
                int maxEnd = 0;
                foreach (var v in columns)
                {
                    int end = v.Start + v.Length;
                    if (end > maxEnd)
                    {
                        maxEnd = end;
                    }
                }

                sb.Append(stmt).Append("if (line.Length < ").Append(maxEnd).AppendLine(") { model = default!; return false; }");
                AppendParseAndReturn(sb, stmt, model, runtimeFqn, elementType, static c => "line.Slice(" + c.Start + ", " + c.Length + ")");
            }
            else
            {
                AppendParseAndReturn(sb, stmt, model, runtimeFqn, elementType, static _ => "default");
            }

            sb.Append(body).AppendLine("}");
        }

        private static void AppendParseAndReturn(
            StringBuilder sb,
            string stmt,
            ModelInfo model,
            string runtimeFqn,
            string elementType,
            Func<ColumnInfo, string> columnExpression)
        {
            var columns = model.Columns.AsImmutableArray();
            for (int i = 0; i < columns.Length; i++)
            {
                var c = columns[i];
                string col = columnExpression(c);

                if (!c.IsNullable)
                {
                    AppendColumnKindParse(sb, stmt, c, col, runtimeFqn, elementType, i, "__v" + i);
                    continue;
                }

                // T?: a blank (trimmed-empty) column assigns null without invoking the underlying
                // parser; otherwise the underlying T parses into a temp local exactly as it would for
                // a non-nullable column, then is boxed into the T? local.
                string trimCall = elementType == "char"
                    ? "global::FixedWidthParser.FixedWidthRuntime.TrimColumn(" + col + ", " + SymbolDisplay.FormatLiteral(c.TrimChar, true) + ", global::FixedWidthParser.Attributes.TrimMode.Both).Trim(' ').IsEmpty"
                    : "global::FixedWidthParser.Utf8FixedWidthRuntime.TrimColumn(" + col + ", " + (c.TrimChar == ' ' ? "(byte)' '" : "__trim" + i) + ", global::FixedWidthParser.Attributes.TrimMode.Both).Trim((byte)' ').IsEmpty";
                sb.Append(stmt).Append(c.TypeFqn).Append("? __v").Append(i).AppendLine(";");
                sb.Append(stmt).Append("if (").Append(trimCall).AppendLine(")");
                sb.Append(stmt).AppendLine("{");
                sb.Append(stmt).Append("    __v").Append(i).AppendLine(" = null;");
                sb.Append(stmt).AppendLine("}");
                sb.Append(stmt).AppendLine("else");
                sb.Append(stmt).AppendLine("{");
                AppendColumnKindParse(sb, stmt + "    ", c, col, runtimeFqn, elementType, i, "__u" + i);
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
        }

        // Emits the kind-specific parse statement for one column into a local named <paramref
        // name="localName"/>. Shared by the non-nullable case (localName = "__v{i}") and the nullable
        // case (localName = "__u{i}", a temp later boxed into the "__v{i}" nullable local) — the
        // converter field is always "__converter{i}" (keyed by column index, not the local's name).
        private static void AppendColumnKindParse(StringBuilder sb, string stmt, ColumnInfo c, string col, string runtimeFqn, string elementType, int i, string localName)
        {
            string trimArg = TrimArgSuffix(elementType, c, i);
            bool isDateTimeType = c.TypeFqn is "global::System.DateTime" or "System.DateTime" or "global::System.DateOnly" or "System.DateOnly" or "global::System.TimeOnly" or "System.TimeOnly" or "global::System.DateTimeOffset" or "System.DateTimeOffset";
            if (c.Format is not null && isDateTimeType)
            {
                string exactHelper = c.TypeFqn.Contains("DateTimeOffset") ? "TryDateTimeOffsetExact"
                                    : c.TypeFqn.Contains("DateTime") ? "TryDateTimeExact"
                                    : c.TypeFqn.Contains("DateOnly") ? "TryDateOnlyExact"
                                    : "TryTimeOnlyExact";

                sb.Append(stmt).Append("if (!").Append(runtimeFqn).Append('.').Append(exactHelper).Append('(').Append(col)
                  .Append(", ").Append(SymbolDisplay.FormatLiteral(c.Format, true)).Append(", formatProvider, out var ").Append(localName).Append(trimArg).AppendLine(")) { model = default!; return false; }");
                return;
            }

            switch (c.Kind)
            {
                case ColumnKind.String:
                    sb.Append(stmt).Append("string ").Append(localName)
                      .Append(" = ").Append(runtimeFqn).Append(".String(").Append(col).Append(", stringPool").Append(trimArg).AppendLine(");");
                    break;
                case ColumnKind.Double:
                    sb.Append(stmt).Append("if (!").Append(runtimeFqn).Append(".TryDouble(").Append(col)
                      .Append(", formatProvider, out double ").Append(localName).Append(trimArg).AppendLine(")) { model = default!; return false; }");
                    break;
                case ColumnKind.Float:
                    sb.Append(stmt).Append("if (!").Append(runtimeFqn).Append(".TryFloat(").Append(col)
                      .Append(", formatProvider, out float ").Append(localName).Append(trimArg).AppendLine(")) { model = default!; return false; }");
                    break;
                case ColumnKind.SpanParsable:
                    sb.Append(stmt).Append("if (!").Append(runtimeFqn).Append(".TryParse<").Append(c.TypeFqn).Append(">(").Append(col)
                      .Append(", formatProvider, out ").Append(c.TypeFqn).Append(' ').Append(localName).Append(trimArg).AppendLine(")) { model = default!; return false; }");
                    break;
                case ColumnKind.Converter:
                    sb.Append(stmt).Append("if (!").Append(runtimeFqn).Append(".TryConvert<").Append(c.TypeFqn).Append(", ").Append(c.ConverterFqn)
                      .Append(">(").Append(col).Append(", formatProvider, __converter").Append(i).Append(", out ").Append(c.TypeFqn)
                      .Append(' ').Append(localName).Append(trimArg).AppendLine(")) { model = default!; return false; }");
                    break;
                default:
                    // Should not happen, already filtered in Emit with a diagnostic.
                    break;
            }
        }

        // Trailing trimChar argument for a runtime parse call: omitted when the column uses the
        // default (space) trim, so unconfigured columns keep emitting the exact same call shape as
        // before this option existed. Char columns pass the literal directly; byte columns reference
        // the precomputed __trim{i} field (AppendUtf8TrimFields) since a non-ASCII char can't be a
        // byte literal — the field does that conversion once, with a clear throw if it doesn't fit.
        private static string TrimArgSuffix(string elementType, ColumnInfo c, int i)
        {
            if (c.TrimChar == ' ' && c.TrimMode == 0)
            {
                return string.Empty;
            }
            string trimArg = elementType == "char"
                ? SymbolDisplay.FormatLiteral(c.TrimChar, true)
                : "__trim" + i;
            return ", " + trimArg + ", " + TrimModeLiteral(c.TrimMode);
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

        private enum ColumnKind { String, Double, Float, SpanParsable, Converter, Unsupported }

        private enum WriteKind { String, Formattable, Converter, Unsupported }

        private readonly struct ColumnInfo(
            string name, int start, int length, ColumnKind kind, string typeFqn, string? converterFqn, bool isNullable, bool isCharParsable, bool isUtf8Parsable,
            WriteKind writeKind, bool isCharFormattable, int alignment, char padding, string? format, int overflow, char trimChar, int trimMode) : IEquatable<ColumnInfo>
        {
            public readonly string Name = name;
            public readonly int Start = start;
            public readonly int Length = length;
            public readonly ColumnKind Kind = kind;
            public readonly string TypeFqn = typeFqn;
            public readonly string? ConverterFqn = converterFqn;
            public readonly bool IsNullable = isNullable;
            public readonly bool IsCharParsable = isCharParsable;
            public readonly bool IsUtf8Parsable = isUtf8Parsable;
            public readonly WriteKind WKind = writeKind;
            public readonly bool IsCharFormattable = isCharFormattable;
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
                       && IsCharParsable == other.IsCharParsable
                       && WKind == other.WKind
                       && IsCharFormattable == other.IsCharFormattable
                       && Alignment == other.Alignment
                       && Padding == other.Padding
                       && Overflow == other.Overflow
                       && TrimChar == other.TrimChar
                       && TrimMode == other.TrimMode
                       && string.Equals(Format, other.Format, StringComparison.Ordinal)
                       && IsUtf8Parsable == other.IsUtf8Parsable
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
                    hash = hash * 31 + (IsCharParsable ? 1 : 0);
                    hash = hash * 31 + (IsUtf8Parsable ? 1 : 0);
                    hash = hash * 31 + (int)WKind;
                    hash = hash * 31 + (IsCharFormattable ? 1 : 0);
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
                                       EquatableArray<ColumnInfo> columns, bool isPartial, bool isNested,
                                       bool implementsChar, bool implementsUtf8, Location location) : IEquatable<ModelInfo>
        {
            public string? Namespace { get; } = ns;
            public string TypeKeywords { get; } = typeKeywords;
            public string TypeName { get; } = typeName;
            public string FullyQualifiedName { get; } = fullyQualifiedName;
            public EquatableArray<ColumnInfo> Columns { get; } = columns;
            public bool IsPartial { get; } = isPartial;
            public bool IsNested { get; } = isNested;
            public bool ImplementsChar { get; } = implementsChar;
            public bool ImplementsUtf8 { get; } = implementsUtf8;
            public Location Location { get; } = location;

            // Value equality (excluding Location, which doesn't affect the emitted source) so the
            // incremental pipeline caches correctly: toggling a marker or a column type re-runs Emit.
            [ExcludeFromCodeCoverage]
            public bool Equals(ModelInfo? other)
            {
                return other is not null
                       && IsPartial == other.IsPartial
                       && IsNested == other.IsNested
                       && ImplementsChar == other.ImplementsChar
                       && ImplementsUtf8 == other.ImplementsUtf8
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
                    hash = hash * 31 + (ImplementsChar ? 1 : 0);
                    hash = hash * 31 + (ImplementsUtf8 ? 1 : 0);
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
