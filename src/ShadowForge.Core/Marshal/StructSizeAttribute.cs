namespace ShadowForge.Marshal;

/// <summary>
/// Declares the wire size of a [BigEndian] struct. The generator emits it as a Size constant
/// and fails with SF002 when the fields run past it. A nested [BigEndian] field needs one.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, Inherited = false)]
public sealed class StructSizeAttribute : Attribute
{
    public int Size { get; }
    public StructSizeAttribute(int size) => Size = size;
}
