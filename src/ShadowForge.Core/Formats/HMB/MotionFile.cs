namespace ShadowForge.Formats.HMB;

/// <summary>
/// The .hmb container, the same one HDB uses: a fixed header, then a first table whose
/// entries select sections by type.
/// </summary>
public static class MotionFile
{
    /// <summary>
    /// Bytes "@HMB" read as a big-endian u32.
    /// </summary>
    public const uint Magic = 0x424D4840;

    public const uint Version = 0x00010000;

    public const int HeaderSize = 0x20;

    /// <summary>
    /// Header field holding the first table's offset, measured from the field's own position.
    /// </summary>
    public const int FirstTableOffsetField = 0x14;

    public static int FirstTableStart(uint storedOffset)
        => (int)storedOffset + FirstTableOffsetField;

    /// <summary>
    /// First-table entries start this far into the table, after the u32 count and a
    /// self-relative u32 offset to the second table.
    /// </summary>
    public const int FirstTableEntriesOffset = 8;

    /// <summary>
    /// Each first-table entry is (type, 0, self-relative offset to the section, section size).
    /// </summary>
    public const int FirstTableEntrySize = 16;

    public const int FirstTableEntryDataField = 8;

    /// <summary>
    /// The motion section header: a self-relative s32 to the track array, then u16 duration,
    /// mode, track count and a zero u16, then f32 rate and rate flag and a zero u32.
    /// </summary>
    public const int MotionHeaderSize = 0x18;

    public const int MotionDurationField = 4;
    public const int MotionModeField = 6;
    public const int MotionTrackCountField = 8;
    public const int MotionRateField = 12;
    public const int MotionRateFlagField = 16;
}
