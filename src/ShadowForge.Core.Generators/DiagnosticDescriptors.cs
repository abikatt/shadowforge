using Microsoft.CodeAnalysis;

namespace ShadowForge.Generators;

internal static class DiagnosticDescriptors
{
    private const string Category = "ShadowForge.Marshal";

    public static readonly DiagnosticDescriptor NotPartial = Error(
        "SF001",
        "Struct must be partial",
        "Struct '{0}' has [BigEndian] but is not declared partial");

    public static readonly DiagnosticDescriptor ExceedsStructSize = Error(
        "SF002",
        "Fields exceed declared struct size",
        "Computed end offset {0} exceeds [StructSize({1})] on '{2}'");

    public static readonly DiagnosticDescriptor OffsetMovesBackward = Error(
        "SF003",
        "Offset moves backward",
        "[Offset({0})] on field '{1}' would move before current position {2}");

    public static readonly DiagnosticDescriptor EncodedStringOnNonByteArray = Error(
        "SF004",
        "EncodedString on non-byte[] field",
        "[EncodedString] on field '{0}' requires type byte[]");

    public static readonly DiagnosticDescriptor UnsupportedFieldType = Error(
        "SF005",
        "Unsupported field type",
        "Field '{0}' has unsupported type '{1}'");

    public static readonly DiagnosticDescriptor GapBetweenFields = new(
        "SF006",
        "Gap between fields",
        "{0} unreachable byte(s) between offset {1} and field '{2}' at offset {3}",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NestedStructMissingBigEndian = Error(
        "SF007",
        "Nested struct missing [BigEndian]",
        "Field '{0}' has struct type '{1}' which is not marked [BigEndian]");

    private static DiagnosticDescriptor Error(string id, string title, string message)
        => new(id, title, message, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);
}
