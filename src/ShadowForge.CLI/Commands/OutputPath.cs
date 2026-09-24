namespace ShadowForge.CLI.Commands;

internal static class OutputPath
{
    public static void CreateParentDirectory(string outputFilePath)
    {
        string? dir = Path.GetDirectoryName(Path.GetFullPath(outputFilePath));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    /// <summary>
    /// Returns <paramref name="requested"/>, or <paramref name="inputPath"/> with
    /// <paramref name="extension"/> when no -o was given, after creating its parent directory.
    /// </summary>
    public static string ForFile(FileInfo? requested, string inputPath, string extension)
    {
        string path = requested?.FullName ?? Path.ChangeExtension(inputPath, extension);
        CreateParentDirectory(path);
        return path;
    }

    /// <summary>
    /// Resolves an -o value. An existing directory or a value ending in a separator gets
    /// "{stem}{defaultExt}" inside it. Otherwise the value is a file path, and
    /// <paramref name="defaultExt"/> is appended when it has no extension.
    /// </summary>
    public static string Resolve(string oValue, string stem, string defaultExt)
    {
        if (NamesDirectory(oValue))
            return Path.Combine(oValue, stem + defaultExt);
        return Path.HasExtension(oValue) ? oValue : oValue + defaultExt;
    }

    public static bool NamesDirectory(string oValue) =>
        Directory.Exists(oValue) || oValue.EndsWith('/') || oValue.EndsWith('\\');
}
