using ShadowForge.Formats.MAP;

namespace ShadowForge.GameData.Maps;

/// <summary>
/// One stage. DisplayName is the game's place name for it (see <see cref="NameTables"/>), or
/// the stage id when the game has none, as for battle arenas and world-map tiles.
/// </summary>
public sealed record MapCatalogEntry(
    string StageId, string Category, string RegionIPK, bool RegionAvailable, int ModelCount, string DisplayName);

public sealed record MapCatalogResult(
    IReadOnlyList<MapCatalogEntry> Stages, IReadOnlyList<string> Warnings);

/// <summary>
/// Lists map stages by joining the pack_map_* manifests with the stage .map defs. A stage def
/// that cannot be found is a warning with ModelCount 0, and a def missing from every manifest
/// is a warning, never a dropped row.
/// </summary>
public sealed class MapCatalog
{
    private readonly GameInstall _install;
    private readonly GameFileSystem _gfs;

    public MapCatalog(GameInstall install)
    {
        _install = install;
        _gfs = new GameFileSystem(install);
    }

    /// <param name="language">The name table to label stages from: "us", "de" or "es".</param>
    public MapCatalogResult List(string language = "us")
    {
        var manifests = PackManifests.Load(_install);
        var stages = new List<MapCatalogEntry>();
        var warnings = new List<string>();
        var names = NameTables.Load(_gfs, language);
        foreach (var (mapFile, regionIPK) in manifests.MapToIPK)
        {
            bool available = new MapRegionReader(_install, regionIPK).Available;
            int modelCount = 0;
            try
            {
                modelCount = StageDef.Read(_gfs.ReadMapDef(mapFile)).Models.Count;
            }
            catch (FileNotFoundException)
            {
                warnings.Add($"stage def not found: {mapFile}");
            }
            string stageId = PackManifests.StageId(mapFile);
            stages.Add(new MapCatalogEntry(stageId,
                manifests.MapCategory.GetValueOrDefault(mapFile, ""), regionIPK, available, modelCount,
                names.Stage(stageId) ?? stageId));
        }

        foreach (string vfs in _gfs.EnumerateVfs(@"database\map", "db_*.map"))
        {
            string file = Path.GetFileName(vfs);
            if (!manifests.MapToIPK.ContainsKey(file))
                warnings.Add($"stage def not in any pack_map manifest: {file}");
        }
        stages.Sort((a, b) => string.Compare(a.StageId, b.StageId, StringComparison.OrdinalIgnoreCase));
        return new MapCatalogResult(stages, warnings);
    }
}
