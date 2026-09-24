using System.Text;

namespace ShadowForge.GameData.Mods;

/// <summary>
/// Builds a modded database file as the vanilla file from the install plus appended rows. A
/// declared row that already exists in the vanilla file, or is declared twice, is rejected, so
/// disabling the mod always restores the vanilla file exactly.
/// </summary>
public sealed class DbOverlay
{
    private readonly GameFileSystem _gfs;

    public DbOverlay(GameInstall install) => _gfs = new GameFileSystem(install);

    public byte[] Apply(string vfsPath, IReadOnlyList<string> rows)
    {
        byte[] baseBytes = _gfs.ReadVfs(vfsPath);
        if (rows.Count == 0) return baseBytes;

        string text = EncodingExtensions.DecodeShiftJISRaw(baseBytes, 0, baseBytes.Length);
        var existing = new HashSet<string>(
            text.Split('\n').Select(l => l.TrimEnd('\r').Trim()), StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sb = new StringBuilder(text);
        if (sb.Length > 0 && sb[^1] != '\n') sb.Append("\r\n");

        foreach (string row in rows)
        {
            string trimmed = row.Trim();
            if (existing.Contains(trimmed))
                throw new InvalidOperationException(
                    $"Row is already present in the vanilla '{vfsPath}': {trimmed}");
            if (!seen.Add(trimmed))
                throw new InvalidOperationException(
                    $"Row is declared twice for '{vfsPath}': {trimmed}");
            sb.Append(trimmed).Append("\r\n");
        }
        return sb.ToString().EncodeShiftJIS();
    }
}
