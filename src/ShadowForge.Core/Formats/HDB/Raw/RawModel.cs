namespace ShadowForge.Formats.HDB.Raw;

/// <summary>
/// Typed form of an HDB file. Every byte ModelReader consumes and ModelWriter emits
/// comes from a field here.
/// </summary>
public sealed class RawModel
{
    public RawHeader Header { get; set; } = new();

    /// <summary>
    /// First-table entries in disk order.
    /// </summary>
    public List<RawEntry> FirstTable { get; set; } = new();

    public RawSecondTable SecondTable { get; set; } = new();

    /// <summary>
    /// A 16-byte record between the aligned IA region start and the first index block
    /// header, present when the u32 at the region start is zero. Empty in most files.
    /// </summary>
    public byte[] PreIaPadding { get; set; } = [];

    /// <summary>
    /// Up to one block per type-3 entry. ModelReader stops at end of file.
    /// </summary>
    public List<RawIndexBlock> IndexBlocks { get; set; } = new();

    /// <summary>
    /// Up to one block per type-5 record, counted across all type-5 entries, in file order.
    /// Records that repeat an Offset still own a block of their own.
    /// </summary>
    public List<RawVertexArray> VertexArrays { get; set; } = new();

    /// <summary>
    /// Bytes past the last VA block, or past the last FT payload when the second table
    /// is empty.
    /// </summary>
    public byte[] TrailingBytes { get; set; } = [];

    internal int LastPayloadEnd => FirstTable.Count == 0 ? 0 : FirstTable.Max(e => e.PayloadEnd);
}
