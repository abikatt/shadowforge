using ShadowForge.Marshal;

namespace ShadowForge.Formats.HDB.Wire;

[BigEndian, StructSize(0x20)]
public partial struct FileHeader
{
    public uint Magic;
    public uint Flags;
    public uint FileSizeHint;
    public uint FormatVersion;
    public uint Reserved10;
    public uint FirstTableOffset;
    public uint Reserved18;
    public uint Reserved1C;
}
