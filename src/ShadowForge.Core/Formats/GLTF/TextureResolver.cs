namespace ShadowForge.Formats.GLTF;

/// <summary>
/// Finds texture files by texture-table name in a directory and its subdirectories,
/// ignoring case.
/// </summary>
public static class TextureResolver
{
    public static string? FindDDS(string textureName, string searchDirectory)
    {
        return Find(textureName + ".dds", "*.dds", searchDirectory);
    }

    /// <summary>
    /// Finds the Xbox 360 3D (.36t) texture for a texture-table name. Fur shells
    /// (voltex_*) ship only in this form, with no .dds.
    /// </summary>
    public static string? FindVolume(string textureName, string searchDirectory)
    {
        return Find(textureName + ".36t", "*.36t", searchDirectory);
    }

    public static string? FindNormalMap(string textureName, string searchDirectory)
    {
        return FindDDS(textureName + "_n", searchDirectory);
    }

    private static string? Find(string target, string pattern, string searchDirectory)
    {
        foreach (var file in Directory.EnumerateFiles(searchDirectory, pattern, SearchOption.AllDirectories))
        {
            if (string.Equals(Path.GetFileName(file), target, StringComparison.OrdinalIgnoreCase))
                return file;
        }
        return null;
    }
}
