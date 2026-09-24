using ShadowForge.Formats.HDB.Wire;

namespace ShadowForge.Formats.HDB.Raw;

/// <summary>
/// Type 10: the texture table, 28 bytes per record.
/// </summary>
public sealed class RawTextureTableEntry : RawEntry
{
    public List<RawTextureRecord> Records { get; set; } = new();
    internal override int ComputedDiskEntryType => (int)FirstTableEntryType.TextureTable;
    internal override int ComputedPayloadLength => Records.Count * TextureData.Size;
}
