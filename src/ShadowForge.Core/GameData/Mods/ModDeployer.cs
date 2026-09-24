using System.Text;

namespace ShadowForge.GameData.Mods;

public sealed record DeployResult(string ModDir, int FilesWritten);

public sealed record ModInfo(string Name, bool Enabled);

/// <summary>
/// Copies a PackPlan into {ModsRoot}\{name} at each entry's override path and lists installed
/// mods. A loose extract has no mods folder, so both need an Install. Asset bytes are copied
/// unchanged. mod.toml is the only file written from scratch.
/// </summary>
public sealed class ModDeployer
{
    private const string AccessLogFolder = "mod_access_logs";

    private readonly GameInstall _install;

    public ModDeployer(GameInstall install) => _install = install;

    public string ModsRoot => _install.ModsRoot
        ?? throw new InvalidOperationException(
            "The located root is a loose extract, not a reblue install - nowhere to deploy a mod.");

    public DeployResult Deploy(PackPlan plan, string modName, ModMetadata? meta = null)
    {
        if (string.IsNullOrWhiteSpace(modName))
            throw new ArgumentException("Mod name must not be empty.", nameof(modName));

        string modDir = Path.Combine(ModsRoot, modName);
        int written = WriteEntries(plan.Entries, modDir);
        if (meta is not null) WriteToml(Path.Combine(modDir, "mod.toml"), meta);
        return new DeployResult(modDir, written);
    }

    /// <summary>
    /// Reads the mod_order.txt that ModOrder.ResolveTarget picks for the profile.
    /// </summary>
    public IReadOnlyList<ModInfo> List(string? profile = null)
    {
        var (orderPath, _) = ModOrder.ResolveTarget(_install, null, profile);
        ModOrder order = ModOrder.Load(orderPath);

        var mods = new List<ModInfo>();
        foreach (string dir in Directory.EnumerateDirectories(ModsRoot))
        {
            string name = Path.GetFileName(dir);
            if (name.Equals(AccessLogFolder, StringComparison.OrdinalIgnoreCase)) continue;
            mods.Add(new ModInfo(name, order.Contains(name)));
        }
        return mods;
    }

    internal static int WriteEntries(IEnumerable<PackEntry> entries, string modDir)
    {
        int written = 0;
        foreach (PackEntry e in entries)
        {
            string dest = VfsPath.Under(modDir, e.OverrideRelPath);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(e.SourcePath, dest, overwrite: true);
            written++;
        }
        return written;
    }

    /// <summary>
    /// Writes the [mod] table with TOML-escaped values. Null fields are left out.
    /// </summary>
    public static void WriteToml(string path, ModMetadata m)
    {
        var sb = new StringBuilder();
        sb.Append("[mod]\r\n");
        sb.Append($"name = {Quote(m.Name)}\r\n");
        if (m.Author is not null)      sb.Append($"author = {Quote(m.Author)}\r\n");
        if (m.Version is not null)     sb.Append($"version = {Quote(m.Version)}\r\n");
        if (m.Description is not null) sb.Append($"description = {Quote(m.Description)}\r\n");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static string Quote(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
