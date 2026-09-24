namespace ShadowForge.Formats.HDB.Raw;

/// <summary>
/// Type 9: the scene descriptor, a list of 24-byte records of six u32 each.
/// </summary>
public sealed class RawUnknownEntry9 : RawEntry
{
    public const int RecordSize = 24;

    public List<uint[]> Records { get; set; } = new();
    internal override int ComputedDiskEntryType => (int)FirstTableEntryType.SceneDesc;
    internal override int ComputedPayloadLength => Records.Count * RecordSize;
}
