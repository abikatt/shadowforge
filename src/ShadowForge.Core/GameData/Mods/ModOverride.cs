using ShadowForge.GameData.Entities;

namespace ShadowForge.GameData.Mods;

/// <summary>
/// The mod-relative path reblue's ModManager serves an entity file from, which is the path the
/// game requests. Rig assets go under chara\{rigClass}\{rigId}\. The model def, battle motion
/// script and motion-command table keep their database paths.
/// </summary>
public static class ModOverride
{
    public static string OverridePath(ResolvedFile f) =>
        f.Role is FileRole.ModelDef or FileRole.MotScr or FileRole.MotCmd
            ? f.LooseRelPath
            : f.VfsPath;

    /// <summary>
    /// model_{id}.mdl, motscr_{id}.csv and mtc_{id}.csv go to their database paths. Any other
    /// name is a rig asset, placed under the rig from the model def PATH, which differs from
    /// the entity's own class and id on a shared rig.
    /// </summary>
    public static string OverridePathFor(ResolvedEntity e, string fileName)
    {
        string name = Path.GetFileName(fileName);
        if (IsModelDef(e, name))  return $@"database\model\chara\{e.Class}\{name}";
        if (IsMotScr(e, name))    return $@"database\battle\motscr\{name}";
        if (IsMotCmd(e, name))    return $@"database\motcmd\{e.Class}\{name}";

        string rigClass = ModPath.Segment(e.RigClass, $"the rig class in the PATH of '{e.Id}'");
        string rigId = ModPath.Segment(e.RigId, $"the rig id in the PATH of '{e.Id}'");
        return $@"chara\{rigClass}\{rigId}\{name}";
    }

    /// <summary>
    /// Model def bindings are checked before the name suffixes, so an overlay .hdb named
    /// *_obj.hdb is an Overlay, not a Skeleton.
    /// </summary>
    public static FileRole RoleFor(ResolvedEntity e, string fileName)
    {
        string name = Path.GetFileName(fileName);
        if (IsModelDef(e, name)) return FileRole.ModelDef;
        if (IsMotScr(e, name))   return FileRole.MotScr;
        if (IsMotCmd(e, name))   return FileRole.MotCmd;

        if (e.ModelDef.ObjectOpts.Any(o => Matches(name, o.HDBFile))) return FileRole.Overlay;
        if (Matches(name, e.ModelDef.ObjectHDB))          return FileRole.Skeleton;
        if (Matches(name, e.ModelDef.TextureOverrideCsv)) return FileRole.TextureOverride;
        if (Matches(name, e.ModelDef.MotPack))            return FileRole.Motion;
        if (name.EndsWith("_obj.hdb", StringComparison.OrdinalIgnoreCase)) return FileRole.Skeleton;
        if (name.EndsWith("_mot.mpk", StringComparison.OrdinalIgnoreCase)) return FileRole.Motion;
        if (name.EndsWith(".36t", StringComparison.OrdinalIgnoreCase))     return FileRole.ShellTexture;
        if (name.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))     return FileRole.Texture;
        return FileRole.Other;
    }

    private static bool Matches(string name, string? binding) =>
        binding is not null
        && name.Equals(Path.GetFileName(binding), StringComparison.OrdinalIgnoreCase);

    private static bool IsModelDef(ResolvedEntity e, string name) =>
        name.Equals(EntityId.ModelDefFileName(e.Id), StringComparison.OrdinalIgnoreCase);
    private static bool IsMotScr(ResolvedEntity e, string name) =>
        name.Equals($"motscr_{e.Id}.csv", StringComparison.OrdinalIgnoreCase);
    private static bool IsMotCmd(ResolvedEntity e, string name) =>
        name.Equals($"mtc_{e.Id}.csv", StringComparison.OrdinalIgnoreCase);
}
