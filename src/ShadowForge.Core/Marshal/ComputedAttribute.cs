namespace ShadowForge.Marshal;

/// <summary>
/// Marks a field whose value the writer derives from other data, such as a size or a
/// relative offset. The generated Read and Write still transfer it like any other field.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class ComputedAttribute : Attribute { }
