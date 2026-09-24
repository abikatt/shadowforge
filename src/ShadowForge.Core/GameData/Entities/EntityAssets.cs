using ShadowForge.Formats.IPK;

namespace ShadowForge.GameData.Entities;

/// <summary>
/// A rig extracted to disk. Skeleton, Motion and TextureOverrideCsv are null when the model
/// def names none or the file is absent from the rig.
/// </summary>
public sealed record RigWorkspace(string Dir, string? Skeleton, string? Motion,
    IReadOnlyList<string> Textures, IReadOnlyList<string> ShellTextures, string? TextureOverrideCsv);

/// <summary>
/// Writes a chara entity's files to a local directory so path-based tools can use files that
/// live inside packed .ipk archives. Works against a packed install or a loose extract.
/// </summary>
public sealed class EntityAssets
{
    private readonly GameInstall _install;
    private readonly GameFileSystem _gfs;
    private readonly EntityResolver _resolver;

    public EntityAssets(GameInstall install)
    {
        _install = install;
        _gfs = new GameFileSystem(install);
        _resolver = new EntityResolver(install);
    }

    /// <summary>
    /// Throws when the entity has no file of that role or the install lacks it.
    /// </summary>
    public string MaterializeFile(string idOrPath, FileRole role, string destDir)
    {
        var e = _resolver.Resolve(idOrPath);
        var f = e.Files.FirstOrDefault(x => x.Role == role)
            ?? throw new FileNotFoundException($"Entity '{e.Id}' has no {role} file to resolve.");

        byte[] bytes = _gfs.ReadVfs(f.VfsPath);
        Directory.CreateDirectory(destDir);
        string dest = Path.Combine(destDir, Path.GetFileName(f.VfsPath));
        File.WriteAllBytes(dest, bytes);
        return dest;
    }

    /// <summary>
    /// Extracts the whole rig archive, not only the files the model def names, so exporters
    /// that look for sibling textures and motion packs find them.
    /// </summary>
    public RigWorkspace MaterializeRig(string idOrPath, string destDir)
    {
        var e = _resolver.Resolve(idOrPath);
        Directory.CreateDirectory(destDir);

        var manifests = PackManifests.Load(_install);
        string ipkName = manifests.IPKForModel(EntityId.ModelDefFileName(e.Id))
            ?? manifests.IPKForRig(e.RigId) ?? $"{e.RigId}.ipk";
        string rigIPK = Path.Combine(_install.GameDataRoot, "pack", "chara", "ipk", ipkName);
        if (File.Exists(rigIPK))
        {
            using var s = File.OpenRead(rigIPK);
            new ArchiveReader(s).ExtractAll(destDir);
        }
        else
        {
            CopyLooseRig(e, destDir);
        }

        string? Locate(string? fileName) =>
            fileName is null ? null : FindAll(destDir, fileName).FirstOrDefault();

        string? RoleFileName(FileRole role) =>
            e.Files.FirstOrDefault(f => f.Role == role) is { } file ? Path.GetFileName(file.VfsPath) : null;

        return new RigWorkspace(destDir,
            Locate(RoleFileName(FileRole.Skeleton)),
            Locate(RoleFileName(FileRole.Motion)),
            FindAll(destDir, "*.dds"),
            FindAll(destDir, "*.36t"),
            Locate(e.ModelDef.TextureOverrideCsv));
    }

    private static List<string> FindAll(string dir, string pattern) =>
        Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();

    private void CopyLooseRig(ResolvedEntity e, string destDir)
    {
        string[] candidates =
        {
            Path.Combine(_install.GameDataRoot, "chara", e.RigClass, e.RigId),
            Path.Combine(_install.GameDataRoot, "chara", "ipk", e.RigId, e.RigClass, e.RigId),
        };
        string src = candidates.FirstOrDefault(Directory.Exists)
            ?? throw new DirectoryNotFoundException(
                $"No rig archive pack\\chara\\ipk\\{e.RigId}.ipk and no loose rig directory for '{e.Id}'.");
        DirectoryTree.CopyContents(src, destDir);
    }
}
