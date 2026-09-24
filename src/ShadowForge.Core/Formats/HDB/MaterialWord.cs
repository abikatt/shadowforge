namespace ShadowForge.Formats.HDB;

/// <summary>
/// A render-command word that binds one texture-table entry to one texture stage:
/// 0x6000 | stage &lt;&lt; 8 | texture index. Stage 0 is the base material. Stages 1 and 2
/// are the iris and eyelid overlays of a staged (eye) draw.
/// </summary>
public readonly record struct MaterialWord(int Stage, int TextureIndex)
{
    public const ushort Base = 0x6000;

    public static MaterialWord Decode(ushort word)
        => new((word >> 8) & 0xF, word & 0xFF);

    public ushort Encode()
        => (ushort)(Base | (Stage << 8) | TextureIndex);

    public static implicit operator ushort(MaterialWord word) => word.Encode();
}
