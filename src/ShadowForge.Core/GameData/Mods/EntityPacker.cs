using ShadowForge.GameData.Entities;

namespace ShadowForge.GameData.Mods;

/// <summary>
/// Turns a directory of edited character assets into a PackPlan. Files are matched by name, so
/// the directory may be flat or mirror any layout. An empty directory or two files with the
/// same override path is an error.
/// </summary>
public sealed class EntityPacker
{
    private static readonly string[] AllowedExtensions =
        { ".hdb", ".mpk", ".dds", ".36t", ".csv", ".mdl" };

    private readonly EntityResolver _resolver;

    public EntityPacker(GameInstall install) => _resolver = new EntityResolver(install);

    public PackPlan BuildPlan(string idOrPath, string editedDir)
    {
        if (!Directory.Exists(editedDir))
            throw new DirectoryNotFoundException($"Edited assets directory not found: {editedDir}");

        ResolvedEntity entity = _resolver.Resolve(idOrPath);

        var entries = new List<PackEntry>();
        var skipped = new List<string>();
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string src in Directory
                     .EnumerateFiles(editedDir, "*", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            if (!AllowedExtensions.Contains(Path.GetExtension(src), StringComparer.OrdinalIgnoreCase))
            {
                skipped.Add(src);
                continue;
            }

            string rel = ModOverride.OverridePathFor(entity, Path.GetFileName(src));
            if (seen.TryGetValue(rel, out string? first))
                throw new InvalidOperationException(
                    $"Two edited files map to the same override path '{rel}': '{first}' and '{src}'. " +
                    "Remove the duplicate so the deploy is unambiguous.");
            seen[rel] = src;
            entries.Add(new PackEntry(src, rel));
        }

        if (entries.Count == 0)
            throw new InvalidOperationException(
                $"No files to pack in '{editedDir}'." +
                (skipped.Count > 0
                    ? $" {skipped.Count} file(s) were skipped as not game-consumable: {string.Join(", ", skipped)}"
                    : string.Empty));

        return new PackPlan(entity.Id, entity.RigId, entries, skipped);
    }
}
