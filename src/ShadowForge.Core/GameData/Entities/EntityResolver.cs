using ShadowForge.Formats.MDL;

namespace ShadowForge.GameData.Entities;

/// <summary>
/// Resolves an entity, by id or by a path to its model_{id}.mdl, into its model def and every
/// file the def references. Rig assets come from the .mdl PATH, not the entity id, so an entity
/// on a shared rig (em072 and bs46 on em001) resolves to the owner's files.
/// </summary>
public sealed class EntityResolver
{
    private readonly GameFileSystem _gfs;

    public EntityResolver(GameInstall install) => _gfs = new GameFileSystem(install);

    public ResolvedEntity Resolve(string idOrPath)
    {
        string id;
        string cls;
        ModelDef mdl;

        if (EntityId.IsEntityId(idOrPath))
        {
            id = idOrPath;
            cls = EntityId.ClassFor(id)
                  ?? throw new ArgumentException($"Unknown entity class for id '{id}'.");
            mdl = ModelDef.Read(ReadModelDef(EntityId.ModelDefVfsPath(cls, id)));
        }
        else
        {
            id = EntityId.FromModelDefFileName(Path.GetFileName(idOrPath))
                 ?? throw new ArgumentException(
                     $"'{idOrPath}' is neither an entity id nor a model_<id>.mdl path.");
            cls = EntityId.ClassFor(id) ?? "ene";
            mdl = ModelDef.ReadFile(idOrPath);
        }
        string modelDefVfs = EntityId.ModelDefVfsPath(cls, id);

        string rigPath = (mdl.Path ?? throw new InvalidDataException($"{modelDefVfs} has no PATH."))
            .Replace('/', '\\').Trim('\\');
        string[] pp = rigPath.Split('\\');
        if (pp.Length < 3 || !pp[0].Equals("chara", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{modelDefVfs} PATH is not chara\\<class>\\<id>\\: '{rigPath}'.");
        string rigClass = pp[1];
        string rigId = pp[2];

        var (mdlIPK, mdlInner) = _gfs.OwningIPK(modelDefVfs);
        var files = new List<ResolvedFile>
        {
            new(FileRole.ModelDef, modelDefVfs, modelDefVfs, mdlIPK, mdlInner, true),
        };

        if (mdl.ObjectHDB is { Length: > 0 } hdb)
            files.Add(RigFile(FileRole.Skeleton, rigPath, rigClass, rigId, hdb));
        if (mdl.MotPack is { Length: > 0 } mpk)
            files.Add(RigFile(FileRole.Motion, rigPath, rigClass, rigId, mpk));
        if (mdl.TextureOverrideCsv is { Length: > 0 } texCsv)
            files.Add(RigFile(FileRole.TextureOverride, rigPath, rigClass, rigId, texCsv));
        foreach (var opt in mdl.ObjectOpts)
            files.Add(RigFile(FileRole.Overlay, rigPath, rigClass, rigId, opt.HDBFile));

        files.Add(DatabaseFile(FileRole.MotScr, $@"database\battle\motscr\motscr_{id}.csv"));
        files.Add(DatabaseFile(FileRole.MotCmd, $@"database\motcmd\{cls}\mtc_{id}.csv"));

        return new ResolvedEntity
        {
            Id = id, Class = cls, RigId = rigId, RigClass = rigClass, ModelDef = mdl,
            ModelDefRelPath = modelDefVfs, Files = files,
        };
    }

    private byte[] ReadModelDef(string vfs)
    {
        try { return _gfs.ReadVfs(vfs); }
        catch (FileNotFoundException ex)
        {
            throw new FileNotFoundException($"Model definition not found: {vfs}", ex);
        }
    }

    private ResolvedFile RigFile(FileRole role, string rigPath, string rigClass, string rigId, string fileName)
    {
        string vfs = $@"{rigPath}\{fileName}";
        string loose = $@"chara\ipk\{rigId}\{rigClass}\{rigId}\{fileName}";
        var (ipkName, inner) = _gfs.OwningIPK(vfs);
        return new ResolvedFile(role, vfs, loose, ipkName, inner, _gfs.ExistsVfs(vfs));
    }

    private ResolvedFile DatabaseFile(FileRole role, string vfs)
    {
        var (ipkName, inner) = _gfs.OwningIPK(vfs);
        return new ResolvedFile(role, vfs, vfs, ipkName, inner, _gfs.ExistsVfs(vfs));
    }
}
