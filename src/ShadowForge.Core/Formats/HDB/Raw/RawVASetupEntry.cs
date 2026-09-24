using ShadowForge.Formats.HDB.Wire;

namespace ShadowForge.Formats.HDB.Raw;

/// <summary>
/// Type 5: a u32 record count followed by 12-byte VA setup records, kept in disk order.
/// </summary>
public sealed class RawVASetupEntry : RawEntry
{
    public List<RawVASetupRecord> Records { get; set; } = new();
    internal override int ComputedDiskEntryType => (int)FirstTableEntryType.VASetup;
    internal override int ComputedPayloadLength => 4 + Records.Count * VASetupRecord.Size;
}
