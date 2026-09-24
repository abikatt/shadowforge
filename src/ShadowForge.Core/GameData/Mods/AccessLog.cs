namespace ShadowForge.GameData.Mods;

public sealed record AccessRow(
    string Pack, string InnerPath, int Accesses, int Overrides, int Misses);

/// <summary>
/// reblue's logs\file_access_summary.csv. It records what the engine requests, which a loose
/// extract's layout does not tell you. Rows with an empty pack or inner path are skipped.
/// </summary>
public static class AccessLog
{
    public static IReadOnlyList<AccessRow> Load(string csvPath)
    {
        var rows = new List<AccessRow>();
        foreach (string raw in File.ReadLines(csvPath).Skip(1))
        {
            var c = raw.TrimEnd('\r').Split(',');
            if (c.Length < 5 || c[0].Length == 0 || c[1].Length == 0) continue;
            rows.Add(new AccessRow(c[0], c[1], ParseCount(c[2]), ParseCount(c[3]), ParseCount(c[4])));
        }
        return rows;
    }

    /// <summary>
    /// Matches a VFS path against each row's pack\inner_path.
    /// </summary>
    public static AccessRow? Find(IReadOnlyList<AccessRow> rows, string vfsPath)
    {
        string needle = VfsPath.Normalize(vfsPath);
        return rows.FirstOrDefault(r =>
            string.Equals(r.Pack + "\\" + r.InnerPath, needle, StringComparison.OrdinalIgnoreCase));
    }

    private static int ParseCount(string s) => int.TryParse(s, out int n) ? n : 0;
}
