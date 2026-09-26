namespace ShadowForge.Formats.HDB;

/// <summary>
/// How a texture repeats outside [0, 1]. Blue Dragon UVs span [-1, +1] on mirror-symmetric
/// parts, so textures sample with mirrored repeat. A mirrortex_* texture, the metal skin of
/// em139, em142 and the other plated enemies, is the exception: its draws shift V by one whole
/// tile into [1, 2] and never go negative, so only plain repeat reaches the half of the image
/// they paint. Mirroring folds them onto the other half instead.
/// </summary>
public static class TextureAddressing
{
    public static bool RepeatsPlainly(string textureName) =>
        textureName.StartsWith("mirrortex", StringComparison.OrdinalIgnoreCase);
}
