using System.Text;

namespace ShadowForge.GameData.Mods;

/// <summary>
/// The mod_order.txt reblue reads to pick active mods: one folder name per line, and a later
/// line wins over an earlier one. Names compare case-insensitively.
/// </summary>
public sealed class ModOrder
{
    private const string FileName = "mod_order.txt";

    private readonly string _path;
    private readonly List<string> _names;

    private ModOrder(string path, List<string> names) { _path = path; _names = names; }

    public IReadOnlyList<string> Names => _names;

    public bool Contains(string name) =>
        _names.Any(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Moves the mod to the last line, removing any other casing of it, so it wins.
    /// </summary>
    public void Enable(string name)
    {
        Disable(name);
        _names.Add(name);
    }

    /// <summary>
    /// Returns true if any casing of the mod was present.
    /// </summary>
    public bool Disable(string name) =>
        _names.RemoveAll(n => n.Equals(name, StringComparison.OrdinalIgnoreCase)) > 0;

    public static ModOrder Load(string path)
    {
        var names = File.Exists(path)
            ? File.ReadAllLines(path).Select(l => l.Trim()).Where(l => l.Length > 0).ToList()
            : new List<string>();
        return new ModOrder(path, names);
    }

    public void Save()
    {
        string? dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        string body = _names.Count == 0 ? "" : string.Join("\r\n", _names) + "\r\n";
        File.WriteAllText(_path, body, new UTF8Encoding(false));
    }

    /// <summary>
    /// Picks the mod_order.txt that governs an install: an explicit path, else
    /// profiles\{profile}\mod_order.txt when that profile folder exists (profile defaults to
    /// "default"), else mods\mod_order.txt. The chosen file may not exist yet. otherExisting is
    /// the candidate not chosen when it exists, so the caller can warn that two files compete.
    /// </summary>
    public static (string chosen, string? otherExisting) ResolveTarget(
        GameInstall install, string? explicitPath = null, string? profile = null)
    {
        if (!string.IsNullOrEmpty(explicitPath))
            return (explicitPath, null);

        string modsRoot = install.ModsRoot
            ?? throw new InvalidOperationException(
                "The located root is a loose extract, not a reblue install - no mods folder to order.");
        string rootFile = Path.Combine(modsRoot, FileName);

        string profileName = string.IsNullOrEmpty(profile) ? "default" : profile;
        string? profileDir = install.InstallRoot is null
            ? null
            : Path.Combine(install.InstallRoot, "profiles", profileName);
        string? profileFile = profileDir is null ? null : Path.Combine(profileDir, FileName);

        if (profileFile is not null && Directory.Exists(profileDir))
            return (profileFile, File.Exists(rootFile) ? rootFile : null);

        return (rootFile,
                profileFile is not null && File.Exists(profileFile) ? profileFile : null);
    }
}
