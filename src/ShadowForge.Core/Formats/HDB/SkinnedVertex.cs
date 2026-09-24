namespace ShadowForge.Formats.HDB;

/// <summary>
/// Field offsets within one packed 100-byte skinned vertex record. Position and normal
/// repeat once per influence. Each influence normal carries the palette index in its
/// fourth short. The three UV slots are the base UV and the two eye overlays.
/// </summary>
public static class SkinnedVertex
{
    public const int Stride = 100;

    public const int Position = 0x00;
    public const int Normal = 0x10;
    public const int Color = 0x18;
    public const int UV = 0x1C;

    /// <summary>
    /// TEXCOORD1: the iris UV, scrolled at runtime by the .mdl UVEYE data. Zero in body records.
    /// </summary>
    public const int UVEye = 0x20;

    /// <summary>
    /// TEXCOORD2: the eyelid overlay UV, addressing the PATCHG frame textures.
    /// </summary>
    public const int UVEyelid = 0x24;

    /// <summary>
    /// Second copy of the base UV in body records.
    /// </summary>
    public const int UVDuplicate = 0x28;

    public const int Position2 = 0x2C;
    public const int Normal2 = 0x3C;
    public const int Position3 = 0x44;
    public const int Normal3 = 0x54;
    public const int Tangent = 0x5C;

    /// <summary>
    /// Offset of the palette index within a per-influence normal.
    /// </summary>
    public const int PaletteIndexInNormal = 6;

    /// <summary>
    /// Quantizes a UV into the non-normalized SHORT4 form the shaders expect, scaling by
    /// 512. The +32 bias keeps typical 0..1 coordinates positive and is a whole number of
    /// wrap periods, so it cancels under a wrapping sampler.
    /// </summary>
    public static short QuantizeUV(float value)
    {
        float raw = (value + 32f) * 512f;
        return (short)Math.Clamp((int)MathF.Round(raw), short.MinValue, short.MaxValue);
    }

    /// <summary>
    /// Inverse of <see cref="QuantizeUV"/>.
    /// </summary>
    public static float DequantizeUV(short value) => value / 512f - 32f;

    /// <summary>
    /// Encodes a local palette index into the fourth short of a per-influence normal, where
    /// the shader reads it after SHORT4N normalization. The high byte holds twice the index.
    /// </summary>
    public static short EncodePaletteIndex(int localPaletteIndex)
        => (short)((localPaletteIndex * 2) << 8);
}
