namespace ShadowForge.Formats.HDB;

/// <summary>
/// One skinning influence. Position and normal are in the influencing bone's local
/// space. The runtime transforms each by its bone and blends the results by weight.
/// Normals are signed 16-bit fixed point, 32767 being +1.0.
/// </summary>
public sealed class BoneInfluence
{
    /// <summary>
    /// Index into the draw's matrix palette, not a bone Index.
    /// </summary>
    public int PaletteIndex { get; set; }
    public float Weight { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public short NormalX { get; set; }
    public short NormalY { get; set; }
    public short NormalZ { get; set; }
}
