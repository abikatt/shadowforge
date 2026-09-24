using ShadowForge.Formats.HDB.Raw;

namespace ShadowForge.Formats.HDB;

/// <summary>
/// Cooked HDB model produced by ModelCooker for the exporters. ModelWriter never reads it.
/// </summary>
public sealed class ModelFile
{
    public RawHeader Header { get; set; } = new();
    public List<Bone> Bones { get; set; } = new();
    public List<TextureEntry> Textures { get; set; } = new();
    public int TextureCount { get; set; }
    public List<VertexArray> VertexArrays { get; set; } = new();
    public List<IndexArray> IndexArrays { get; set; } = new();

    /// <summary>
    /// One command list per type-3 entry, in reverse disk order. Chunks are not flattened
    /// because a chunk does not always end in a decodable 0x00 0xFF marker, and the VA
    /// index of each chunk is relative to that chunk.
    /// </summary>
    public List<List<RenderCommand>> RenderCommandChunks { get; set; } = new();

    public List<MeshGroup> MeshGroups { get; set; } = new();
    public SecondTableData SecondTable { get; set; } = new();
}
