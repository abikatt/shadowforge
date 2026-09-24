namespace ShadowForge.Formats.DDS;

internal static class GraphicFormatExtensions
{
    public static bool IsBlockCompressed(this GraphicFormat format)
        => format is GraphicFormat.TextureFormatDXT1
            or GraphicFormat.TextureFormatDXT3
            or GraphicFormat.TextureFormatDXT5
            or GraphicFormat.TextureFormatDXN;

    /// <summary>
    /// Bytes per 4x4 block for block-compressed formats, bytes per pixel otherwise.
    /// </summary>
    public static int BytesPerUnit(this GraphicFormat format) => format switch
    {
        GraphicFormat.TextureFormatDXT1 => 8,
        GraphicFormat.TextureFormatDXT3 or GraphicFormat.TextureFormatDXT5 or GraphicFormat.TextureFormatDXN => 16,
        GraphicFormat.TextureFormatA8R8G8B8 => 4,
        GraphicFormat.TextureFormatA8L8 or GraphicFormat.TextureFormatX4R4G4B4 or GraphicFormat.TextureFormatR5G6B5 => 2,
        GraphicFormat.TextureFormatL8 => 1,
        _ => throw new ArgumentException($"Unsupported format: {format}"),
    };

    /// <summary>
    /// Units along one axis: 4x4 blocks, rounded up, for block-compressed formats, pixels otherwise.
    /// </summary>
    public static int UnitsAcross(this GraphicFormat format, int pixels)
        => format.IsBlockCompressed() ? (pixels + 3) / 4 : pixels;
}
