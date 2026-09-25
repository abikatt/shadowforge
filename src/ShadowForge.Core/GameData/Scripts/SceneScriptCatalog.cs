using System.Text.RegularExpressions;
using ShadowForge.Formats.RPJ;
using ShadowForge.Scene;

namespace ShadowForge.GameData.Scripts;

/// <summary>
/// One field scene script. VfsPath is where the game reads it (script\town\sc01_0001_01.rpj),
/// which is also where a mod overrides it. StageId is the stage it drives, or null when it
/// names a stage the install does not list.
/// </summary>
public sealed record SceneScriptEntry(string VfsPath, string Area, string FileName, string SceneName, string? StageId);

/// <summary>
/// Lists the scene scripts (script\{area}\*.rpj) and ties each to its stage. The header's
/// message path names the stage's dialogue table (mes_bg01_01.u16), which is the most
/// reliable link; the tool that wrote it often left junk after the name, so the stage id is
/// matched rather than parsed. Without a usable message path, the stage id is built from the
/// area type and the header's StageId (dungeon 1204 is dg12_04).
/// </summary>
public sealed class SceneScriptCatalog
{
    private const string ScriptDir = "script";
    private static readonly Regex MessageStage = new(@"mes_([a-z]{2}\d{2}[a-z]?_?\d{2})", RegexOptions.IgnoreCase);

    private readonly GameFileSystem _files;

    public SceneScriptCatalog(GameFileSystem files) => _files = files;

    /// <param name="knownStages">The install's stage ids, as MapCatalog lists them.</param>
    public IReadOnlyList<SceneScriptEntry> List(IReadOnlySet<string> knownStages)
    {
        var entries = new List<SceneScriptEntry>();
        foreach (string vfs in _files.EnumerateVfs(ScriptDir, "*.rpj"))
        {
            SceneFile scene;
            try
            {
                scene = BinaryScene.Read(_files.ReadVfs(vfs));
            }
            catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentException
                                           or IndexOutOfRangeException)
            {
                continue;
            }
            string[] parts = vfs.Split('\\');
            entries.Add(new SceneScriptEntry(vfs, parts.Length > 2 ? parts[1] : "",
                Path.GetFileNameWithoutExtension(vfs), scene.SceneName,
                StageFor(scene.MessagePath, scene.AreaType, scene.StageId, knownStages)));
        }
        return entries;
    }

    internal static string? StageFor(string messagePath, AreaType area, uint stageId, IReadOnlySet<string> knownStages)
    {
        if (MessageStage.Match(messagePath) is { Success: true } m && knownStages.Contains(m.Groups[1].Value))
            return m.Groups[1].Value.ToLowerInvariant();

        string? prefix = area switch
        {
            AreaType.Town => "bg",
            AreaType.Dungeon => "dg",
            AreaType.World => "wd",
            AreaType.Cube => "wc",
            _ => null,
        };
        if (prefix is null) return null;
        string guess = $"{prefix}{stageId / 100:00}_{stageId % 100:00}";
        return knownStages.Contains(guess) ? guess : null;
    }
}
