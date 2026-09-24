using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ShadowForge.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class BigEndianStructGenerator : IIncrementalGenerator
{
    private const string BigEndianFullName = "ShadowForge.Marshal.BigEndianAttribute";
    private const string StructSizeFullName = "ShadowForge.Marshal.StructSizeAttribute";
    private const string EncodedStringFullName = "ShadowForge.Marshal.EncodedStringAttribute";
    private const string OffsetFullName = "ShadowForge.Marshal.OffsetAttribute";
    private const string SkipFullName = "ShadowForge.Marshal.SkipAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var structs = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                BigEndianFullName,
                predicate: (node, _) => node is StructDeclarationSyntax,
                transform: (ctx, ct) => ExtractStructInfo(ctx, ct));

        context.RegisterSourceOutput(structs, Execute);
    }

    private static StructInfo ExtractStructInfo(
        GeneratorAttributeSyntaxContext ctx,
        System.Threading.CancellationToken ct)
    {
        var symbol = (INamedTypeSymbol)ctx.TargetSymbol;
        var syntax = (StructDeclarationSyntax)ctx.TargetNode;

        List<FieldInfo> fields = [];
        foreach (var member in symbol.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (member is IFieldSymbol fs &&
                !fs.IsStatic &&
                fs.DeclaredAccessibility == Accessibility.Public &&
                !fs.IsConst)
            {
                fields.Add(AnalyzeField(fs));
            }
        }

        return new StructInfo
        {
            Namespace = symbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : symbol.ContainingNamespace.ToDisplayString(),
            TypeName = symbol.Name,
            DeclaredSize = DeclaredStructSize(symbol),
            IsPartial = syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)),
            Fields = fields,
            Location = syntax.Identifier.GetLocation(),
        };
    }

    private static FieldInfo AnalyzeField(IFieldSymbol fs)
    {
        var info = new FieldInfo
        {
            Name = fs.Name,
            TypeName = fs.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            IsByteArray = fs.Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte },
        };

        foreach (var attr in fs.GetAttributes())
        {
            if (MatchesFullName(attr, SkipFullName))
                info.IsSkipped = true;
            if (MatchesFullName(attr, OffsetFullName) && attr.ConstructorArguments.Length == 1)
            {
                info.Offset = attr.ConstructorArguments[0].Value as int? ?? 0;
                info.HasExplicitOffset = true;
            }
            if (MatchesFullName(attr, EncodedStringFullName) && attr.ConstructorArguments.Length >= 1)
                info.EncodedStringLength = attr.ConstructorArguments[0].Value as int? ?? 0;
        }

        Classify(fs.Type, info);
        return info;
    }

    private static void Classify(ITypeSymbol type, FieldInfo info)
    {
        if (info.EncodedStringLength > 0)
        {
            info.Kind = FieldKind.EncodedString;
            info.Size = info.EncodedStringLength;
            return;
        }

        if (TryPrimitive(type.SpecialType, out string keyword, out int size))
        {
            info.Kind = FieldKind.Primitive;
            info.Keyword = keyword;
            info.Size = size;
            return;
        }

        switch (type.SpecialType)
        {
            case SpecialType.System_Single:
                info.Kind = FieldKind.Float;
                info.Size = 4;
                return;
            case SpecialType.System_Double:
                info.Kind = FieldKind.Double;
                info.Size = 8;
                return;
        }

        if (info.TypeName == "global::System.Numerics.Vector3")
        {
            info.Kind = FieldKind.Vector3;
            info.Size = 12;
            return;
        }

        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum, EnumUnderlyingType: { } underlying } &&
            TryPrimitive(underlying.SpecialType, out keyword, out size))
        {
            info.Kind = FieldKind.Enum;
            info.Keyword = keyword;
            info.Size = size;
            return;
        }

        info.Kind = FieldKind.Unsupported;
        if (type.TypeKind == TypeKind.Struct && type is INamedTypeSymbol nested)
        {
            info.NestedStructFullName = info.TypeName;
            int nestedSize = DeclaredStructSize(nested);
            if (HasAttribute(nested, BigEndianFullName) && nestedSize > 0)
            {
                info.Kind = FieldKind.Nested;
                info.Size = nestedSize;
            }
        }
    }

    private static bool TryPrimitive(SpecialType type, out string keyword, out int size)
    {
        (keyword, size) = type switch
        {
            SpecialType.System_Byte => ("byte", 1),
            SpecialType.System_UInt16 => ("ushort", 2),
            SpecialType.System_Int16 => ("short", 2),
            SpecialType.System_UInt32 => ("uint", 4),
            SpecialType.System_Int32 => ("int", 4),
            SpecialType.System_UInt64 => ("ulong", 8),
            SpecialType.System_Int64 => ("long", 8),
            _ => ("", 0),
        };
        return size > 0;
    }

    private static void Execute(SourceProductionContext spc, StructInfo info)
    {
        if (!info.IsPartial)
        {
            spc.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.NotPartial, info.Location, info.TypeName));
            return;
        }

        int currentOffset = 0;
        bool hasError = false;
        List<FieldInfo> resolvedFields = [];

        foreach (var field in info.Fields)
        {
            if (field.IsSkipped)
                continue;

            if (field.HasExplicitOffset)
            {
                if (field.Offset < currentOffset)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.OffsetMovesBackward, info.Location,
                        field.Offset, field.Name, currentOffset));
                    hasError = true;
                    continue;
                }

                if (field.Offset > currentOffset)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.GapBetweenFields, info.Location,
                        field.Offset - currentOffset, currentOffset, field.Name, field.Offset));
                }

                currentOffset = field.Offset;
            }

            if (field.Kind == FieldKind.EncodedString && !field.IsByteArray)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.EncodedStringOnNonByteArray, info.Location, field.Name));
                hasError = true;
                continue;
            }

            if (field.Kind == FieldKind.Unsupported)
            {
                var descriptor = field.NestedStructFullName != null
                    ? DiagnosticDescriptors.NestedStructMissingBigEndian
                    : DiagnosticDescriptors.UnsupportedFieldType;
                spc.ReportDiagnostic(Diagnostic.Create(descriptor, info.Location, field.Name, field.TypeName));
                hasError = true;
                continue;
            }

            resolvedFields.Add(field.At(currentOffset));
            currentOffset += field.Size;
        }

        if (info.DeclaredSize > 0 && currentOffset > info.DeclaredSize)
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ExceedsStructSize, info.Location,
                currentOffset, info.DeclaredSize, info.TypeName));
            hasError = true;
        }

        if (hasError)
            return;

        spc.AddSource(info.TypeName + ".Marshal.g.cs", MarshalSourceWriter.Write(info, resolvedFields));
    }

    private static int DeclaredStructSize(INamedTypeSymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (MatchesFullName(attr, StructSizeFullName) && attr.ConstructorArguments.Length == 1)
                return attr.ConstructorArguments[0].Value as int? ?? 0;
        }
        return 0;
    }

    private static bool HasAttribute(INamedTypeSymbol symbol, string fullName)
        => symbol.GetAttributes().Any(a => MatchesFullName(a, fullName));

    private static bool MatchesFullName(AttributeData attr, string fullName)
        => attr.AttributeClass?.ToDisplayString() == fullName;
}
