using ShadowForge.GameData;
using ShadowForge.GameData.Entities;

namespace ShadowForge.CLI.Commands;

/// <summary>
/// Resolves "path or entity id" arguments. An entity id (pc01, em001) is looked up in the
/// install and its files are copied to a temp directory. Anything else is a plain path, so a
/// file literally named like an id can be passed as ".\pc01".
/// </summary>
internal static class EntityArg
{
    /// <summary>
    /// Resolves the file for <paramref name="role"/>. With <paramref name="withSiblings"/>,
    /// an id extracts the whole rig so the motion pack and textures sit next to the file.
    /// Locates an install without overlays, so a caller holding an overlay-aware install must
    /// use the <see cref="GameInstall"/> overload.
    /// </summary>
    public static ResolvedArg File(string? gameRoot, string arg, FileRole role, bool withSiblings) =>
        EntityId.IsEntityId(arg) ? File(GameInstall.Locate(gameRoot), arg, role, withSiblings) : FromPath(arg);

    public static ResolvedArg File(GameInstall install, string arg, FileRole role, bool withSiblings)
    {
        if (!EntityId.IsEntityId(arg)) return FromPath(arg);
        return FromEntity(arg, temp =>
        {
            var assets = new EntityAssets(install);
            if (!withSiblings)
                return [assets.MaterializeFile(arg, role, temp.FullName)];
            var rig = assets.MaterializeRig(arg, temp.FullName);
            string? path = role switch
            {
                FileRole.Skeleton => rig.Skeleton,
                FileRole.Motion => rig.Motion,
                _ => null,
            };
            return [path ?? throw new FileNotFoundException($"Entity '{arg}' has no {role} file.")];
        });
    }

    /// <summary>
    /// Resolves every texture of an entity id, or the single texture path given.
    /// </summary>
    public static ResolvedArg Textures(string? gameRoot, string arg)
    {
        if (!EntityId.IsEntityId(arg)) return FromPath(arg);
        var install = GameInstall.Locate(gameRoot);
        return FromEntity(arg, temp =>
        {
            var rig = new EntityAssets(install).MaterializeRig(arg, temp.FullName);
            if (rig.Textures.Count == 0)
                throw new FileNotFoundException($"Entity '{arg}' has no textures to export.");
            return rig.Textures;
        });
    }

    private static ResolvedArg FromPath(string arg) => new()
    {
        Paths = [arg], Stem = Path.GetFileNameWithoutExtension(arg), FromEntity = false,
    };

    private static ResolvedArg FromEntity(string id, Func<TempDir, IReadOnlyList<string>> materialize)
    {
        var temp = new TempDir(id);
        try
        {
            return new ResolvedArg { Paths = materialize(temp), Stem = id, FromEntity = true, Temp = temp };
        }
        catch
        {
            temp.Dispose();
            throw;
        }
    }
}
