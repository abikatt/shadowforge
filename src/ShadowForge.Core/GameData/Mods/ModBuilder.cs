using ShadowForge.GameData.Entities;

namespace ShadowForge.GameData.Mods;

public sealed record BuildResult(string ModDir, int FilesWritten, IReadOnlyList<string> Entities);

/// <summary>
/// Turns a ModSource into a mod folder. Every entity is staged first and the staging tree is
/// added as an overlay, because the resolver must see model defs the install does not have
/// before any entity can be packed. Db files are merged after every entity has added its
/// rows, so each file is written once.
/// </summary>
public sealed class ModBuilder
{
    private readonly GameInstall _install;

    public ModBuilder(GameInstall install) => _install = install;

    public BuildResult Build(ModSource source, string modsRoot)
    {
        string modDir = Path.Combine(modsRoot, ModPath.Segment(source.Name, "the mod name"));
        string staging = Path.Combine(Path.GetTempPath(), "sforge",
            "build-" + Guid.NewGuid().ToString("N"));

        try
        {
            var produced = new List<(SourceEntity Entity, string Dir)>();
            foreach (SourceEntity e in source.Entities)
                produced.Add((e, StageEntity(e, staging)));

            var install = _install.WithOverlays(
                new[] { StagingVfsRoot(staging) }.Concat(_install.Overlays).ToList());

            int written = 0;
            foreach (var (entity, dir) in produced)
                written += ModDeployer.WriteEntries(
                    new EntityPacker(install).BuildPlan(entity.Id, dir).Entries, modDir);

            written += MergeDb(install, source, modDir);

            ModDeployer.WriteToml(Path.Combine(modDir, "mod.toml"),
                new ModMetadata(source.Name, null, null, source.Description));

            return new BuildResult(modDir, written,
                source.Entities.Select(e => e.Id).ToList());
        }
        finally
        {
            DirectoryTree.DeleteStaging(staging);
        }
    }

    private string StageEntity(SourceEntity e, string staging)
    {
        string dir = Directory.CreateDirectory(Path.Combine(staging, "entities", e.Id)).FullName;

        if (e.DeriveFrom is { } from)
            new EntityDeriver(_install).Derive(from, e.Id, e.Class, dir);

        foreach (string authored in Directory.EnumerateFiles(e.Dir))
        {
            if (Path.GetFileName(authored).Equals(ModSource.EntityToml, StringComparison.OrdinalIgnoreCase))
                continue;
            File.Copy(authored, Path.Combine(dir, Path.GetFileName(authored)), overwrite: true);
        }

        if (e.MotScrTemplate is { } template)
        {
            File.WriteAllBytes(Path.Combine(dir, $"motscr_{e.Id}.csv"),
                new MotScrGenerator(_install).Generate(template, e.Id));
        }

        string mdlName = EntityId.ModelDefFileName(e.Id);
        string mdl = Path.Combine(dir, mdlName);
        if (!File.Exists(mdl))
            throw new FileNotFoundException(
                $"Entity '{e.Id}' produced no {mdlName}. Declare [derive] or author one.");

        string mirror = VfsPath.Under(StagingVfsRoot(staging), EntityId.ModelDefVfsPath(e.Class, e.Id));
        Directory.CreateDirectory(Path.GetDirectoryName(mirror)!);
        File.Copy(mdl, mirror, overwrite: true);
        return dir;
    }

    private static string StagingVfsRoot(string staging) => Path.Combine(staging, "vfs");

    private static int MergeDb(GameInstall install, ModSource source, string modDir)
    {
        var byFile = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (SourceEntity e in source.Entities)
            foreach (var (vfs, row) in e.Db)
            {
                string key = ModPath.Relative(vfs, $"the db key declared by entity '{e.Id}'");
                if (!byFile.TryGetValue(key, out var rows))
                    byFile[key] = rows = [];
                rows.Add(row);
            }

        var overlay = new DbOverlay(install);
        foreach (var (key, rows) in byFile)
        {
            string vfs = VfsPath.Normalize(key);
            string dest = VfsPath.Under(modDir, vfs);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.WriteAllBytes(dest, overlay.Apply(vfs, rows));
        }
        return byFile.Count;
    }
}
