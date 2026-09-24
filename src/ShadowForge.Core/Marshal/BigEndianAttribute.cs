namespace ShadowForge.Marshal;

/// <summary>
/// Generates static big-endian Read and Write methods over the struct's public instance
/// fields, laid out in declaration order with no implicit padding. The struct must be partial.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, Inherited = false)]
public sealed class BigEndianAttribute : Attribute { }
