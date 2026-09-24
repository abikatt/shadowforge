namespace ShadowForge.Formats.HDB.Raw;

/// <summary>
/// One first-table entry. Subclasses carry the typed payload. The Disk* fields
/// record where the payload sits so ModelWriter can re-emit it at the same position.
/// </summary>
public abstract class RawEntry
{
    /// <summary>
    /// Offset of the DataOffset field inside a first-table entry. Payload offsets are
    /// relative to this field, not to the entry start.
    /// </summary>
    public const int DataOffsetField = 8;

    /// <summary>
    /// The u32 at entry+0x00. Kept explicitly so unknown entries remember their type code.
    /// </summary>
    public int DiskEntryType { get; set; }

    /// <summary>
    /// Payload offset relative to this entry's +0x08 field.
    /// </summary>
    public int DiskOffset { get; set; }

    public int DiskLength { get; set; }

    /// <summary>
    /// Absolute file position of this 16-byte first-table record.
    /// </summary>
    public int AbsoluteEntryPos { get; set; }

    public int PayloadPos => AbsoluteEntryPos + DataOffsetField + DiskOffset;

    public int PayloadEnd => PayloadPos + DiskLength;

    /// <summary>
    /// Type code implied by the subclass. RawLayout.Compute copies it into DiskEntryType.
    /// </summary>
    internal abstract int ComputedDiskEntryType { get; }

    /// <summary>
    /// Payload length implied by the typed content. RawLayout.Compute copies it into DiskLength.
    /// </summary>
    internal abstract int ComputedPayloadLength { get; }
}
