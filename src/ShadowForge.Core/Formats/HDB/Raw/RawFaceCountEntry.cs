namespace ShadowForge.Formats.HDB.Raw;

/// <summary>
/// Type 4: index count of the paired index block.
/// </summary>
public sealed class RawFaceCountEntry : RawEntry
{
    public uint Count { get; set; }

    /// <summary>
    /// Runtime index buffer handle cell at +0x04. Read and written only when DiskLength is at least 8.
    /// </summary>
    public uint IndexBufferHandleSlot { get; set; }

    internal override int ComputedDiskEntryType => (int)FirstTableEntryType.IndexTable;
    internal override int ComputedPayloadLength => 8;
}
