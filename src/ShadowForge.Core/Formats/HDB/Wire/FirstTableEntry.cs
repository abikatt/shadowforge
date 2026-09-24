using ShadowForge.Marshal;

namespace ShadowForge.Formats.HDB.Wire;

[BigEndian, StructSize(0x10)]
public partial struct FirstTableEntry
{
    public uint EntryType;
    public uint Zero;
    public uint DataOffset;
    public uint DataLength;
}
