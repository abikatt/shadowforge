using ShadowForge.Marshal;

namespace ShadowForge.Formats.HDB.Wire;

[BigEndian, StructSize(0x10)]
public partial struct VAHeaderData
{
    public uint ByteSize;
    public uint FormatType;
    public uint VertexCount;
    public uint Reserved0C;
}
