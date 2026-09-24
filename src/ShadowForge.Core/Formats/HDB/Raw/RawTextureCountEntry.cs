namespace ShadowForge.Formats.HDB.Raw;

/// <summary>
/// Type 11: texture count plus a pointer back to the type-10 table.
/// </summary>
public sealed class RawTextureCountEntry : RawEntry
{
    public uint Count { get; set; }

    /// <summary>
    /// Signed offset from this cell (+0x04) to the type-10 payload. Read and written only
    /// when DiskLength is at least 8.
    /// </summary>
    public uint OffsetToTextureTable { get; set; }

    internal override int ComputedDiskEntryType => (int)FirstTableEntryType.TextureCount;
    internal override int ComputedPayloadLength => 8;
}
