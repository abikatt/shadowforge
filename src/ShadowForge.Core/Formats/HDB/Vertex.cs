namespace ShadowForge.Formats.HDB;

/// <summary>
/// A decoded vertex. Normals are signed 16-bit fixed point, 32767 being +1.0.
/// </summary>
public sealed class Vertex
{
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }

    public short NormalX { get; set; }
    public short NormalY { get; set; }
    public short NormalZ { get; set; }

    public float U { get; set; }
    public float V { get; set; }

    /// <summary>
    /// Iris UV (TEXCOORD1), scrolled at runtime by the .mdl UVEYE data. Zero in body records.
    /// </summary>
    public float UEye { get; set; }
    public float VEye { get; set; }

    /// <summary>
    /// Eyelid overlay UV (TEXCOORD2), addressing the PATCHG frame textures. Zero in body records.
    /// </summary>
    public float UEyelid { get; set; }
    public float VEyelid { get; set; }

    public List<BoneInfluence> Influences { get; set; } = new();
}
