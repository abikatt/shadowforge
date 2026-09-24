using Microsoft.CodeAnalysis;

namespace ShadowForge.Generators;

internal sealed class StructInfo
{
    public string? Namespace { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public int DeclaredSize { get; set; }
    public bool IsPartial { get; set; }
    public List<FieldInfo> Fields { get; set; } = [];
    public Location Location { get; set; } = Location.None;
}
