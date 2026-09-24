namespace ShadowForge.GameData.Entities;

/// <summary>
/// One browsable entity. DisplayName is the id, since the game has no name table to read.
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
    public IReadOnlyList<CatalogEntry> ListChara()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<CatalogEntry>();
        foreach (string vfs in _gfs.EnumerateVfs(CharaModelDir, "model_*.mdl"))
        {
            string[] segs = vfs.Split('\\');
            if (segs.Length < 2) continue;
            if (EntityId.FromModelDefFileName(segs[^1]) is not { } id) continue;
            if (!seen.Add(id)) continue;
            list.Add(new CatalogEntry(id, "chara", segs[^2], vfs, id));
        }
        list.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
        return list;
    }
}
