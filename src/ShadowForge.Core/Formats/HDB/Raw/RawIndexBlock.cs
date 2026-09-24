namespace ShadowForge.Formats.HDB.Raw;

/// <summary>
/// One index block: a 16-byte header, big-endian u16 indices and trailing pad. Per-draw
/// ranges are sliced out by the IA-select commands of the owning type-3 entry.
/// </summary>
public sealed class RawIndexBlock
{
    /// <summary>
    /// Body size in bytes, indices plus pad. The index count is ByteSize / 2.
    /// </summary>
    public uint ByteSize { get; set; }

    /// <summary>
    /// Kept verbatim and not used for sizing.
    /// </summary>
    public uint ByteSizeDuplicate { get; set; }

    public uint Reserved08 { get; set; }
    public uint Reserved0C { get; set; }

    public ushort[] Indices { get; set; } = [];

    /// <summary>
    /// Body bytes past the last whole index.
    /// </summary>
    public byte[] TrailingPad { get; set; } = [];
}
