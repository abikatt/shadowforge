using System.Text.RegularExpressions;
using ShadowForge.GameData.Entities;

namespace ShadowForge.GameData;

/// <summary>
/// The tables the game selects archives by: pack_chr_model.txt (model def to chara rig IPK)
/// and pack_map_{category}.txt (stage .map to map region IPK), both two-column double-quoted
/// CSV. Each is looked for under pack\, then !necessity\pack\, then the loose-extract
/// location. A missing table yields an empty map, since loose extracts may not carry them.
/// </summary>
public sealed partial class PackManifests
{
    public IReadOnlyDictionary<string, string> CharaModelToIPK { get; }
    public IReadOnlyDictionary<string, string> MapToIPK { get; }
    public IReadOnlyDictionary<string, string> MapCategory { get; }

    private PackManifests(Dictionary<string, string> chara,
        Dictionary<string, string> map, Dictionary<string, string> cat)
    { CharaModelToIPK = chara; MapToIPK = map; MapCategory = cat; }

    public string? IPKForModel(string modelFileName) =>
        CharaModelToIPK.TryGetValue(modelFileName, out var ipk) ? ipk : null;

    /// <summary>
    /// The archive holding a rig directory (chara\{class}\{rigId}\...), found through the
    /// rig's own model row.
    /// </summary>
    public string? IPKForRig(string rigId) => IPKForModel(EntityId.ModelDefFileName(rigId));

    public string? IPKForMap(string stageOrMapFile) =>
        MapToIPK.TryGetValue(NormalizeMapKey(stageOrMapFile), out var ipk) ? ipk : null;

    /// <summary>
    /// VFS candidates for a stage def, most likely first. Defs are shelved under database\map
    /// by manifest category: battle to map\battle, event to map\event, world and cube to
    /// map\world, and dungeon, indoor and town flat under map\. The remaining directories
    /// follow so a def shelved elsewhere still resolves.
    /// </summary>
    public IReadOnlyList<string> MapDefVfsCandidates(string stageOrMapFile)
    {
        string key = NormalizeMapKey(stageOrMapFile);
        string primary = MapCategory.GetValueOrDefault(key, "") switch
        {
            "battle" => @"map\battle",
            "event" => @"map\event",
            "world" or "cube" => @"map\world",
            _ => "map",
        };
        var dirs = new List<string> { primary };
        foreach (string dir in new[] { "map", @"map\battle", @"map\event", @"map\world" })
            if (!dirs.Contains(dir)) dirs.Add(dir);
        return dirs.Select(d => $@"database\{d}\{key}").ToList();
    }

    /// <summary>
    /// "bg01_01", "db_bg01_01" and "db_bg01_01.map" all become "db_bg01_01.map".
    /// </summary>
    public static string NormalizeMapKey(string s)
    {
        string k = s.Trim();
        if (!k.StartsWith("db_", StringComparison.OrdinalIgnoreCase)) k = "db_" + k;
        if (!k.EndsWith(".map", StringComparison.OrdinalIgnoreCase)) k += ".map";
        return k;
    }

    /// <summary>
    /// "db_bg01_01.map" becomes "bg01_01".
    /// </summary>
    internal static string StageId(string mapFile)
    {
        string id = Path.GetFileNameWithoutExtension(mapFile);
        return id.StartsWith("db_", StringComparison.OrdinalIgnoreCase) ? id[3..] : id;
    }

    [GeneratedRegex(@"^pack_map_([a-z0-9]+)\.txt$", RegexOptions.IgnoreCase)]
    private static partial Regex MapManifestName();

    public static PackManifests Load(GameInstall install)
    {
        string root = install.GameDataRoot;

        var chara = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? charaPath = FirstExisting(
            Path.Combine(root, "pack", "chara", "pack_chr_model.txt"),
            Path.Combine(root, "!necessity", "pack", "chara", "pack_chr_model.txt"),
            Path.Combine(root, "chara", "pack_chr_model.txt"));
        if (charaPath is not null) chara = ParseCsv(ReadManifestText(charaPath));

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var cat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? mapDir = FirstExistingMapDir(
            Path.Combine(root, "pack", "map"),
            Path.Combine(root, "!necessity", "pack", "map"),
            Path.Combine(root, "map"));
        if (mapDir is not null)
        {
            foreach (string file in Directory.EnumerateFiles(mapDir, "pack_map_*.txt"))
            {
                var m = MapManifestName().Match(Path.GetFileName(file));
                if (!m.Success) continue;
                string category = m.Groups[1].Value.ToLowerInvariant();
                foreach (var (key, ipk) in ParseCsv(ReadManifestText(file)))
                {
                    map[key] = ipk;
                    cat[key] = category;
                }
            }
        }
        return new PackManifests(chara, map, cat);
    }

    /// <summary>
    /// Rows with fewer than two fields or an empty field are skipped.
    /// </summary>
    internal static Dictionary<string, string> ParseCsv(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var parts = line.Split(',', 2);
            if (parts.Length != 2) continue;
            string key = parts[0].Trim().Trim('"');
            string val = parts[1].Trim().Trim('"');
            if (key.Length > 0 && val.Length > 0) result[key] = val;
        }
        return result;
    }

    private static string ReadManifestText(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return EncodingExtensions.DecodeShiftJISRaw(bytes, 0, bytes.Length);
    }

    private static string? FirstExisting(params string[] paths) =>
        paths.FirstOrDefault(File.Exists);

    private static string? FirstExistingMapDir(params string[] paths) =>
        paths.FirstOrDefault(p => Directory.Exists(p)
            && Directory.EnumerateFiles(p, "pack_map_*.txt").Any());
}
