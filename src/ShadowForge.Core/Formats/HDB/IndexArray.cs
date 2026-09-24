namespace ShadowForge.Formats.HDB;

/// <summary>
/// The index range one IA-select draws from its chunk's index block.
/// </summary>
public sealed class IndexArray
{
    /// <summary>
    /// First index of the range within the index block.
    /// </summary>
    public int Start { get; set; }

    public int MaterialIndex { get; set; }

    /// <summary>
    /// Stage-1 texture index bound at this draw (0x61NN), or -1. See <see cref="MeshGroup.Stage1TexIndex"/>.
    /// </summary>
    public int Stage1TexIndex { get; set; } = -1;

    /// <summary>
    /// Stage-2 texture index bound at this draw (0x62NN), or -1. See <see cref="MeshGroup.Stage1TexIndex"/>.
    /// </summary>
    public int Stage2TexIndex { get; set; } = -1;

    public ushort[] Indices { get; set; } = [];
}
