namespace ShadowForge.Marshal;

/// <summary>
/// Excludes the field from the wire layout. It occupies no bytes and keeps its default on Read.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class SkipAttribute : Attribute { }
