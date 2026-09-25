using ShadowForge.Formats.BDSL;
using ShadowForge.Formats.RPJ;
using ShadowForge.GameData;
using ShadowForge.GameData.Mods;
using ShadowForge.GameData.Scripts;

namespace ShadowForge.Workbench;

public sealed record ScriptRow(SceneScriptEntry Entry, string Status)
{
    public string FileName => Entry.FileName;

    /// <summary>
    /// The developers' scene label, without the control and private-use characters (their own
    /// Shift-JIS glyphs) that no font can show.
    /// </summary>
    public string SceneName => new(Entry.SceneName
        .Where(c => !char.IsControl(c) && c != '�'
                    && char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.PrivateUse)
        .ToArray());
}

/// <summary>
/// The result of opening a script for editing. RoundTrips is false when the shipped script
/// does not compile back to the same bytes, so an edit of it may lose data.
/// </summary>
public sealed record ScriptOpenResult(string Path, bool Created, bool RoundTrips);

/// <summary>
/// Edited scene scripts as BDSL text under a working folder that mirrors the game's layout
/// ({root}\town\sc01_0001_01.bdsl), and their build into a mod at the path the game reads
/// ({mods}\{name}\script\town\sc01_0001_01.rpj). An edit already on disk is never replaced.
/// </summary>
public sealed class SceneScriptWorkspace
{
    private readonly GameFileSystem _files;

    public string Root { get; }

    public SceneScriptWorkspace(GameFileSystem files, string root)
    {
        _files = files;
        Root = root;
    }

    public string WorkPath(SceneScriptEntry script) => Path.Combine(Root, script.Area, script.FileName + ".bdsl");

    public bool IsEdited(SceneScriptEntry script) => File.Exists(WorkPath(script));

    /// <summary>
    /// Decompiles the shipped script into the working folder unless an edit is already there.
    /// </summary>
    public ScriptOpenResult Open(SceneScriptEntry script)
    {
        string path = WorkPath(script);
        byte[] shipped = _files.ReadVfs(script.VfsPath);
        bool roundTrips = RoundTrips(shipped);
        if (File.Exists(path)) return new ScriptOpenResult(path, Created: false, roundTrips);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        TextScene.Write(BinaryScene.Read(shipped), path);
        return new ScriptOpenResult(path, Created: true, roundTrips);
    }

    /// <summary>
    /// Compiles the edited copy. A syntax error throws with the compiler's message.
    /// </summary>
    public byte[] Compile(SceneScriptEntry script) => BinaryScene.Write(TextScene.ReadFile(WorkPath(script)));

    /// <summary>
    /// Writes the compiled edit into the mod, adding a mod.toml when the mod has none.
    /// Returns the file written.
    /// </summary>
    public string Deploy(SceneScriptEntry script, string modsRoot, string modName)
    {
        byte[] compiled = Compile(script);
        string modDir = Path.Combine(modsRoot, modName);
        string dest = ModFilePath(script, modsRoot, modName);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.WriteAllBytes(dest, compiled);

        string toml = Path.Combine(modDir, "mod.toml");
        if (!File.Exists(toml))
            ModDeployer.WriteToml(toml, new ModMetadata(modName, null, null, "Scene script edits"));
        return dest;
    }

    public static bool IsInMod(SceneScriptEntry script, string modsRoot, string modName) =>
        File.Exists(ModFilePath(script, modsRoot, modName));

    /// <summary>
    /// Where a mod carries the script: the game's own path under the mod folder.
    /// </summary>
    public static string ModFilePath(SceneScriptEntry script, string modsRoot, string modName) =>
        Path.Combine(modsRoot, modName, script.VfsPath);

    public bool ShippedRoundTrips(SceneScriptEntry script) => RoundTrips(_files.ReadVfs(script.VfsPath));

    private static bool RoundTrips(byte[] shipped)
    {
        try
        {
            var text = TextScene.Write(BinaryScene.Read(shipped));
            return BinaryScene.Write(TextScene.Read(text)).AsSpan().SequenceEqual(shipped);
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException or InvalidDataException)
        {
            return false;
        }
    }
}
