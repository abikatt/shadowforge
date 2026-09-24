namespace ShadowForge.Formats.HDB;

/// <summary>
/// Field offsets within one packed 48-byte rigid vertex record: a single unskinned
/// position and normal, vertex color, two UVs and a tangent.
/// </summary>
public static class RigidVertex
{
    public const int Stride = 48;

    public const int Position = 0x00;
    public const int Normal = 0x0C;
    public const int Color = 0x14;
    public const int UV = 0x18;
    public const int UV2 = 0x20;
    public const int Tangent = 0x28;
}
