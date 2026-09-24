using ShadowForge.Marshal;

namespace ShadowForge.Formats.HDB.Wire;

[BigEndian, StructSize(0x1C)]
public partial struct TextureData
{
    public const int NameWidth = 20;

    [EncodedString(20, StringEncoding.ASCII)]
    public byte[] Name;
    public float FloatParam;
    public uint FlagField18;
}
