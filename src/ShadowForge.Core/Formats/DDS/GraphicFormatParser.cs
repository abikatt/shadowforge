namespace ShadowForge.Formats.DDS;

public static class GraphicFormatParser
{
    /// <summary>
    /// Accepts the DXT, BCn and channel-layout names, case-insensitive: DXT1/BC1, DXT3/BC2,
    /// DXT5/BC3, DXN/BC5, A8R8G8B8/ARGB, R5G6B5/RGB565, X4R4G4B4/ARGB4, A8L8, L8.
    /// </summary>
    public static GraphicFormat Parse(string name)
        => TryParse(name, out var format)
            ? format
            : throw new ArgumentException($"Unknown texture format: {name}");

    public static bool TryParse(string name, out GraphicFormat format)
    {
        GraphicFormat? parsed = name.ToUpperInvariant() switch
        {
            "DXT1" or "BC1" => GraphicFormat.TextureFormatDXT1,
            "DXT3" or "BC2" => GraphicFormat.TextureFormatDXT3,
            "DXT5" or "BC3" => GraphicFormat.TextureFormatDXT5,
            "DXN" or "BC5" => GraphicFormat.TextureFormatDXN,
            "A8R8G8B8" or "ARGB" => GraphicFormat.TextureFormatA8R8G8B8,
            "R5G6B5" or "RGB565" => GraphicFormat.TextureFormatR5G6B5,
            "X4R4G4B4" or "ARGB4" => GraphicFormat.TextureFormatX4R4G4B4,
            "A8L8" => GraphicFormat.TextureFormatA8L8,
            "L8" => GraphicFormat.TextureFormatL8,
            _ => null,
        };
        format = parsed ?? default;
        return parsed.HasValue;
    }
}
