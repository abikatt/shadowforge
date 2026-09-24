using ShadowForge.Marshal;

namespace ShadowForge.Formats.HDB.Wire;

[BigEndian, StructSize(0x10)]
public partial struct IndexBlockHeader
{
    public uint ByteSize;
    public uint ByteSizeDuplicate;
    public uint Reserved08;
    public uint Reserved0C;
}
