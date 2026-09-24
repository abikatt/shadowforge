namespace ShadowForge.GameData;

/// <summary>
/// Game paths are backslash-separated and relative to the game-data root or to a mod folder.
/// The game requests a file by the same path a mod overrides it at.
/// </summary>
internal static class VfsPath
{
    public static string Normalize(string path) => path.Replace('/', '\\').TrimStart('\\');

    public static string Under(string root, string vfsPath) =>
        Path.Combine(root, vfsPath.Replace('\\', Path.DirectorySeparatorChar));
}
