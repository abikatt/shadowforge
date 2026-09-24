using ShadowForge.Marshal;

namespace ShadowForge.Formats.HDB.Wire;

[BigEndian, StructSize(0x0C)]
public partial struct VASetupRecord
{
    public uint VertexCount;
    public uint FormatType;
    public uint Offset;
}
