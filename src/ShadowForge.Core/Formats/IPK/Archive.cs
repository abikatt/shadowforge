namespace ShadowForge.Formats.IPK;

/// <summary>
/// An IPK1 archive header and entry table. The format is little-endian: a 16-byte header
/// (magic, alignment, file count, archive size) followed at 0x10 by one 96-byte record per entry.
/// </summary>
public sealed class Archive
{
    /// <summary>"IPK1" read as a little-endian u32.</summary>
    public const uint Magic = 0x314B5049;

    public const int HeaderSize = 0x10;
    public const int EntryRecordSize = 96;
    public const int NameFieldSize = 0x40;

    public uint Alignment { get; set; }
    public uint FileCount { get; set; }
    public uint ArchiveSize { get; set; }
    public IReadOnlyList<FileEntry> Entries { get; set; } = [];

    /// <summary>
    /// True when compressed entries hold zlib streams, false for LZSS. The header has no flag
    /// for this: it is inferred from the first entry record, whose last three u32s are all zero
    /// in zlib archives.
    /// </summary>
    public bool UsesZlib { get; set; }
}
