namespace ShadowForge.Formats.HDB;

/// <summary>
/// One draw: a vertex array, an index range, a material and a matrix palette.
/// </summary>
public sealed class MeshGroup
{
    /// <summary>
    /// Index into ModelFile.VertexArrays, already offset past the arrays of earlier chunks.
    /// </summary>
    public int VAIndex { get; set; }

    public int IAIndex { get; set; }
    public int MaterialIndex { get; set; }

    /// <summary>
    /// The IA-select opcode (0x10, 0x20 or 0x30) of this draw.
    /// </summary>
    public int Topology { get; set; }

    /// <summary>
    /// Bone Index values addressed by <see cref="BoneInfluence.PaletteIndex"/>.
    /// </summary>
    public List<ushort> BonePalette { get; set; } = new();

    /// <summary>
    /// Stage-1 texture index bound at this draw (0x61NN), or -1. A stage word stays bound
    /// across draws until the next stage-0 (0x60NN) bind resets both stages to -1. Retail
    /// np114 keeps stage words set across consecutive eye draws, and retail pc03 rebinds
    /// 0x6005 right after its eye block.
    /// </summary>
    public int Stage1TexIndex { get; set; } = -1;

    /// <summary>
    /// Stage-2 texture index bound at this draw (0x62NN), or -1. Bound and reset like
    /// <see cref="Stage1TexIndex"/>.
    /// </summary>
    public int Stage2TexIndex { get; set; } = -1;
}
