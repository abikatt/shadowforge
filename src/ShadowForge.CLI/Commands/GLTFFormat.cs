namespace ShadowForge.CLI.Commands;

internal static class GLTFFormat
{
    public const string Supported = "Supported: glb, gltf.";

    /// <summary>
    /// Returns ".glb" or ".gltf" for a --format value, or null when the value is unknown.
    /// With no --format, a .glb or .gltf extension on <paramref name="outputPath"/> decides,
    /// and ".glb" is the default.
    /// </summary>
    public static string? Resolve(string? format, string? outputPath = null)
    {
        if (!string.IsNullOrWhiteSpace(format))
        {
            string f = format.Trim().ToLowerInvariant();
            return f is "glb" or "gltf" ? "." + f : null;
        }
        if (!string.IsNullOrEmpty(outputPath) && !OutputPath.NamesDirectory(outputPath))
        {
            string ext = Path.GetExtension(outputPath).ToLowerInvariant();
            if (ext is ".glb" or ".gltf") return ext;
        }
        return ".glb";
    }
}
