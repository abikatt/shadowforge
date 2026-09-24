namespace ShadowForge.Formats.HDB.Raw;

/// <summary>
/// The 32-byte file header.
/// </summary>
public sealed class RawHeader
{
    /// <summary>
    /// The bytes "BDH@" as a big-endian u32. The byte-swapped form 0x40484442 also
    /// loads. Retail files carry this one.
    /// </summary>
    public const uint MagicValue = 0x42444840;

    /// <summary>
    /// Header field holding the first table's offset, measured from the field itself.
    /// </summary>
    public const int FirstTableOffsetField = 0x14;

    /// <summary>
    /// Absolute position of the first table: { count:u32, relativeRebaseTable:i32,
    /// 16-byte entries }.
    /// </summary>
    public static int FirstTableStart(uint storedOffset)
        => (int)storedOffset + FirstTableOffsetField;

    public uint Magic { get; set; } = MagicValue;
    public uint Flags { get; set; }

    /// <summary>
    /// Correlates with file size. The loader uses it as an allocation hint before parsing.
    /// </summary>
    public uint FileSizeHint { get; set; }

    /// <summary>
    /// 4 in retail files.
    /// </summary>
    public uint FormatVersion { get; set; }

    public uint Reserved10 { get; set; }
    public uint FirstTableOffset { get; set; }
    public uint Reserved18 { get; set; }
    public uint Reserved1C { get; set; }
}
