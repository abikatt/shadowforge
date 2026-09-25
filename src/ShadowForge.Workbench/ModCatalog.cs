using ShadowForge.GameData;
using ShadowForge.GameData.Mods;
using Tomlyn;
using Tomlyn.Model;

namespace ShadowForge.Workbench;

public sealed record ModRow(string Name, bool Enabled, bool FolderExists, string Detail)
{
    public bool Missing => !FolderExists;

    /// <summary>
    /// What a screen reader announces for the list item.
    /// </summary>
    public override string ToString() => $"{Name}, {Detail}";
}

public sealed record ModFileRow(string Path, string Verdict);

public sealed record ModDetails(
    string Title, string? Byline, string? Description,
    IReadOnlyList<ModFileRow> Files, int TotalFiles, bool Traced);

/// <summary>
/// The Mods tab's view of a reblue install: every folder under mods\ joined with the
/// mod_order.txt that governs it, so an entry whose folder is gone still shows up and can be
/// removed. Enabled mods come first in load order, where a later mod wins over an earlier one.
/// </summary>
public sealed class ModCatalog
{
    private const string AccessLogFolder = "mod_access_logs";

    private readonly GameInstall _install;

    public string ModsRoot { get; }
    public string OrderPath { get; }

    /// <summary>
    /// The other mod_order.txt when both the profile's and the mods folder's exist. Only
    /// <see cref="OrderPath"/> is read and written.
    /// </summary>
    public string? OtherOrderPath { get; }

    /// <summary>
    /// reblue's logs\file_access_summary.csv, when the game has been run with logging.
    /// </summary>
    public string? AccessLogPath { get; }

    public ModCatalog(GameInstall install)
    {
        _install = install;
        ModsRoot = new ModDeployer(install).ModsRoot;
        (OrderPath, OtherOrderPath) = ModOrder.ResolveTarget(install);
        string? log = install.InstallRoot is null
            ? null
            : Path.Combine(install.InstallRoot, "logs", "file_access_summary.csv");
        AccessLogPath = log is not null && File.Exists(log) ? log : null;
    }

    public IReadOnlyList<ModRow> List()
    {
        var order = ModOrder.Load(OrderPath).Names;
        var folders = new ModDeployer(_install).List()
            .Select(m => m.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rows = new List<ModRow>();
        for (int i = 0; i < order.Count; i++)
        {
            bool exists = folders.Remove(order[i]);
            string detail = exists
                ? $"Enabled · loads {i + 1} of {order.Count}" + (i == order.Count - 1 && order.Count > 1 ? ", wins over all" : "")
                : "In mod_order.txt, but the folder is missing";
            rows.Add(new ModRow(order[i], Enabled: true, exists, detail));
        }
        rows.AddRange(folders
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(name => new ModRow(name, Enabled: false, FolderExists: true, "Disabled")));
        return rows;
    }

    /// <summary>
    /// Enabling moves the mod to the end of the order, so it wins over every other mod.
    /// </summary>
    public void SetEnabled(string name, bool enabled)
    {
        var order = ModOrder.Load(OrderPath);
        if (enabled) order.Enable(name);
        else order.Disable(name);
        order.Save();
    }

    /// <summary>
    /// The mod.toml fields and up to <paramref name="maxFiles"/> of the mod's files, each with
    /// what the access log says the game did with it when there is a log.
    /// </summary>
    public ModDetails Details(string name, int maxFiles)
    {
        string dir = Path.Combine(ModsRoot, name);
        if (!Directory.Exists(dir))
            return new ModDetails(name, null, "The folder for this mod no longer exists.", [], 0, false);

        var (title, byline, description) = ReadToml(Path.Combine(dir, "mod.toml"), name);
        var rows = AccessLogPath is null ? null : AccessLog.Load(AccessLogPath);
        var files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(dir, f).Replace('/', '\\'))
            .Where(p => !p.Equals("mod.toml", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var shown = files.Take(maxFiles).Select(p => new ModFileRow(p, Verdict(rows, p))).ToList();
        return new ModDetails(title, byline, description, shown, files.Count, rows is not null);
    }

    /// <summary>
    /// Copies a mod folder into mods\ under its own name, left disabled. An installed mod of the
    /// same name is never overwritten.
    /// </summary>
    public string Install(string sourceDir)
    {
        string source = Path.GetFullPath(sourceDir).TrimEnd('\\', '/');
        string name = Path.GetFileName(source);
        string dest = Path.Combine(ModsRoot, name);
        if (name.Equals(AccessLogFolder, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"'{name}' is reserved for reblue's access logs.");
        string modsRoot = Path.GetFullPath(ModsRoot).TrimEnd('\\', '/') + "\\";
        if ((source + "\\").StartsWith(modsRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("That folder is already inside the mods folder.");
        if (Directory.Exists(dest))
            throw new IOException($"A mod named '{name}' is already installed.");

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(dest, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        Directory.CreateDirectory(dest);
        return name;
    }

    private static string Verdict(IReadOnlyList<AccessRow>? rows, string gamePath)
    {
        if (rows is null) return "";
        var hit = AccessLog.Find(rows, gamePath);
        return hit is null ? "never requested"
            : hit.Overrides > 0 ? "overridden"
            : hit.Misses > 0 ? "missed" : "loaded";
    }

    /// <summary>
    /// The [mod] table ModDeployer writes. A missing or malformed file falls back to the
    /// folder name, since mod.toml is optional to reblue.
    /// </summary>
    private static (string Title, string? Byline, string? Description) ReadToml(string path, string fallback)
    {
        if (!File.Exists(path)) return (fallback, null, null);
        try
        {
            if (Toml.ToModel(File.ReadAllText(path)).TryGetValue("mod", out var node) && node is TomlTable mod)
            {
                string? Get(string key) => mod.TryGetValue(key, out var v) && v is string s && s.Length > 0 ? s : null;
                string? byline = string.Join(" · ", new[] { Get("author"), Get("version") is { } v ? "v" + v : null }
                    .Where(s => s is not null));
                return (Get("name") ?? fallback, byline.Length > 0 ? byline : null, Get("description"));
            }
        }
        catch (TomlException)
        {
            return (fallback, null, "mod.toml could not be read.");
        }
        return (fallback, null, null);
    }
}
