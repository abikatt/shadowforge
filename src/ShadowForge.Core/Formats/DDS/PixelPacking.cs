namespace ShadowForge.Formats.DDS;

/// <summary>
/// 16-bit packed pixel forms and luminance. Channels scale by integer multiply then
/// truncating divide in both directions.
/// </summary>
internal static class PixelPacking
{
    public static ushort Pack565(byte r, byte g, byte b)
        => (ushort)(((r * 31 / 255) << 11) | ((g * 63 / 255) << 5) | (b * 31 / 255));

    public static (byte R, byte G, byte B) Unpack565(ushort packed)
        => ((byte)(((packed >> 11) & 0x1F) * 255 / 31),
            (byte)(((packed >> 5) & 0x3F) * 255 / 63),
            (byte)((packed & 0x1F) * 255 / 31));

    /// <summary>
    /// X4R4G4B4 with the top nibble left zero.
    /// </summary>
    public static ushort Pack444(byte r, byte g, byte b)
        => (ushort)(((r * 15 / 255) << 8) | ((g * 15 / 255) << 4) | (b * 15 / 255));

    public static (byte R, byte G, byte B) Unpack444(ushort packed)
        => ((byte)(((packed >> 8) & 0xF) * 255 / 15),
            (byte)(((packed >> 4) & 0xF) * 255 / 15),
            (byte)((packed & 0xF) * 255 / 15));

    /// <summary>
    /// Rec. 601 luma, truncated.
    /// </summary>
    public static byte Luminance(byte r, byte g, byte b)
        => (byte)(0.299 * r + 0.587 * g + 0.114 * b);
}
