using ShadowForge.Formats.HDB.Wire;
using ShadowForge.IO;

namespace ShadowForge.Formats.HDB.Raw;

/// <summary>
/// Assigns disk positions to a RawModel whose typed content is filled in. Sets
/// Header.FirstTableOffset, each entry's AbsoluteEntryPos, DiskEntryType, DiskOffset and
/// DiskLength, and the expected bone pointers. Payloads are packed in FT order, so a file
/// whose payloads are out of FT order round-trips semantically but not byte for byte.
/// </summary>
public static class RawLayout
{
    /// <summary>
    /// Count and byte-length words that precede the first-table entries.
    /// </summary>
    public const int FirstTableHeaderSize = 8;

    /// <summary>
    /// First 16-aligned offset strictly after the type-9 payload, where the index-block and
    /// vertex-array region begins. An already aligned end still moves up 16 bytes.
    /// </summary>
    public static int DataRegionStart(int type9PayloadEndOffset)
        => type9PayloadEndOffset - (type9PayloadEndOffset % 16) + 16;

    public static void Compute(RawModel raw)
    {
        raw.Header.FirstTableOffset = (uint)(FileHeader.Size - RawHeader.FirstTableOffsetField);

        int ftCount = raw.FirstTable.Count;
        int ftSize = FirstTableHeaderSize + ftCount * FirstTableEntry.Size;
        int stSize = 4 + 4 + raw.SecondTable.Entries.Length * 4 + 4;

        int payloadCursor = Align.Up(FileHeader.Size + ftSize + stSize, 16);
        for (int i = 0; i < ftCount; i++)
        {
            var entry = raw.FirstTable[i];
            entry.AbsoluteEntryPos = FileHeader.Size + FirstTableHeaderSize + i * FirstTableEntry.Size;
            entry.DiskEntryType = entry.ComputedDiskEntryType;
            entry.DiskLength = entry.ComputedPayloadLength;
            entry.DiskOffset = payloadCursor - (entry.AbsoluteEntryPos + RawEntry.DataOffsetField);
            payloadCursor += entry.DiskLength;
        }

        var layout = BoneLayout.Build(raw);
        for (int ft = 0; ft < layout.Bones.Count; ft++)
        {
            layout.Bones[ft].ExpectedChildPtrDisk = layout.ComputeChildPtr(ft);
            layout.Bones[ft].ExpectedSiblingPtrDisk = layout.ComputeSiblingPtr(ft);
        }
    }
}
