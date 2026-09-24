namespace ShadowForge.GameData.Mods;

/// <summary>
/// Checks every authored value a build turns into a path under the mod folder: the mod name,
/// a db key, and the rig class and id from a model def PATH. Path.Combine drops its first
/// argument when the second is rooted, and a .. component climbs out of the mod folder into
/// the install, so a rooted, empty or .. value is rejected, never repaired.
/// </summary>
public static class ModPath
{
    /// <summary>
    /// Returns the value unchanged when it is one folder name.
    /// </summary>
    public static string Segment(string value, string origin)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Rejected {origin}: it is empty and must name one folder.");
        if (value.AsSpan().IndexOfAny('\\', '/', ':') >= 0)
            throw new InvalidOperationException(
                $"Rejected {origin}: '{value}' contains a path separator and must name one folder.");
        if (value is "." or "..")
            throw new InvalidOperationException(
                $"Rejected {origin}: '{value}' walks outside the mod folder.");
        return value;
    }

    /// <summary>
    /// Returns the value unchanged when it is a path inside the mod folder.
    /// </summary>
    public static string Relative(string value, string origin)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Rejected {origin}: it is empty and must be a path inside the mod folder.");

        string normalized = value.Replace('/', '\\');

        if (normalized.StartsWith('\\') || normalized.Contains(':') || Path.IsPathRooted(normalized))
            throw new InvalidOperationException(
                $"Rejected {origin}: '{value}' is absolute and must be relative to the mod folder.");
        if (normalized.Split('\\').Contains(".."))
            throw new InvalidOperationException(
                $"Rejected {origin}: '{value}' walks outside the mod folder with '..'.");
        return value;
    }
}
