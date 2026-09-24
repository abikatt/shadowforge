namespace ShadowForge.Formats.HDB.Import;

public sealed class ImportTriangle
{
    public int A, B, C;
    public int MaterialIndex;

    /// <summary>
    /// Index into <see cref="ImportScene.Textures"/> of the iris texture, or -1 for
    /// unstaged triangles. A staged material's baseColor image is its stage-1 texture.
    /// </summary>
    public int Stage1Index = -1;

    /// <summary>
    /// Index of the eyelid overlay texture, or -1 when the material binds none.
    /// </summary>
    public int Stage2Index = -1;
}
