namespace ShadowForge.GameData.Entities;

/// <summary>
/// One browsable entity. DisplayName is the game's own name for it (see <see cref="NameTables"/>),
/// or the id when the game has none.
/// </summary>
public sealed record CatalogEntry(string Id, string Category, string Class, string ModelDefRelPath, string DisplayName);

/// <summary>
/// Lists chara entities by scanning model defs across the loose tree and the packs.
/// </summary>
public sealed class EntityCatalog
{
    private const string CharaModelDir = @"database\model\chara";
    private readonly GameFileSystem _gfs;

    public EntityCatalog(GameInstall install) => _gfs = new GameFileSystem(install);

    /// <summary>
    /// Deduplicated by id and sorted by id.
    /// </summary>
    /// <param name="language">The name table to label entries from: "us", "de" or "es".</param>
    public IReadOnlyList<CatalogEntry> ListChara(string language = "us")
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<CatalogEntry>();
        var names = NameTables.Load(_gfs, language);
        foreach (string vfs in _gfs.EnumerateVfs(CharaModelDir, "model_*.mdl"))
        {
            string[] segs = vfs.Split('\\');
            if (segs.Length < 2) continue;
            if (EntityId.FromModelDefFileName(segs[^1]) is not { } id) continue;
            if (!seen.Add(id)) continue;
            list.Add(new CatalogEntry(id, "chara", segs[^2], vfs, names.Character(id) ?? id));
        }
        list.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
        return list;
    }
}
