namespace ShadowForge.Scene.Script;

public sealed record ArgSpec(string Name, ArgKind Kind, IReadOnlyDictionary<uint, string>? EnumNames = null);
