using System.Globalization;
using System.Text.Json;
using ShadowForge.Formats.GLTF;
using ShadowForge.Formats.HDB;
using ShadowForge.Formats.MAP;
using ShadowForge.Manifests;

namespace ShadowForge.GameData.Maps;

public sealed record MapExportResult(
    string GlbPath, string ManifestPath, IReadOnlyList<string> Warnings);

/// <summary>
/// Exports one map stage to a GLB plus .sfmap.json. Every MODEL block becomes a node named
/// after it under AREA_{area}/PRI_{pri}. Transforms stay identity because stage HDBs are
/// baked in world space. Sidecar parts and HDBs missing from the region are listed in the
/// manifest as skipped, never dropped.
/// </summary>
public sealed class MapExporter
{
    private readonly GameInstall _install;
    private readonly GameFileSystem _gfs;

    public MapExporter(GameInstall install)
    {
        _install = install;
        _gfs = new GameFileSystem(install);
    }

    public MapExportResult Export(string stageId, string outDir, bool includeTextures = true,
        Action<string>? progress = null)
    {
        Directory.CreateDirectory(outDir);
        var manifests = PackManifests.Load(_install);
        string mapKey = PackManifests.NormalizeMapKey(stageId);
        string stage = PackManifests.StageId(mapKey);
        string regionIPK = manifests.IPKForMap(mapKey)
            ?? throw new FileNotFoundException(
                $"Stage '{stageId}' is not in any pack_map_<category>.txt manifest.");
        string category = manifests.MapCategory.GetValueOrDefault(mapKey, "");

        var map = StageDef.Read(_gfs.ReadMapDef(mapKey));
        var region = new MapRegionReader(_install, regionIPK);
        if (!region.Available)
            throw new FileNotFoundException(
                $"Region pack {regionIPK} for stage '{stage}' is not present (packed or loose).");

        string extractDir = Path.Combine(Path.GetTempPath(), "sforge", "map-" + Guid.NewGuid().ToString("N"));
        try
        {
            progress?.Invoke($"extracting region {regionIPK}...");
            region.ExtractAll(extractDir);

            var modelEntries = new List<MapModelEntry>();
            var skipped = new List<MapSkipEntry>();
            var placements = new List<RigidPlacement>();

            int done = 0;
            foreach (var m in map.Models)
            {
                progress?.Invoke($"baking model {++done}/{map.Models.Count}: {m.Name}");
                string? hdbPath = m.ObjectHDB is null ? null : FindInExtract(extractDir, m.ObjectHDB);
                if (hdbPath is null)
                {
                    skipped.Add(new MapSkipEntry("MODEL", m.ObjectHDB ?? m.Name, "hdb-not-found-in-region"));
                }
                else
                {
                    var model = ModelCooker.Bake(ModelReader.Read(File.ReadAllBytes(hdbPath)));
                    string pri = m.Pri.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', '_');
                    placements.Add(new RigidPlacement(
                        Path.GetFileNameWithoutExtension(m.Name), model, $"AREA_{m.Area}/PRI_{pri}"));
                }
                modelEntries.Add(new MapModelEntry(m.Name, m.ObjectHDB ?? "", m.Area, m.Pri, hdbPath is not null));
            }
            foreach (var p in map.Parts)
                skipped.Add(new MapSkipEntry(p.Kind, p.Path, "sidecar-not-imported-v1"));

            string glbPath = Path.Combine(outDir, stage + ".glb");
            progress?.Invoke($"writing {stage}.glb ({placements.Count} models)...");
            var warnings = SceneExporter.ExportMany(placements, glbPath,
                textureDir: includeTextures ? extractDir : null, embed: true).ToList();

            var manifest = new MapManifest(1, stage, category, regionIPK,
                modelEntries, skipped, new MapExportInfo(Path.GetFileName(glbPath), "Y", "meter"));
            string manifestPath = Path.Combine(outDir, stage + ".sfmap.json");
            File.WriteAllText(manifestPath,
                JsonSerializer.Serialize(manifest, ManifestJson.Default.MapManifest));
            return new MapExportResult(glbPath, manifestPath, warnings);
        }
        finally
        {
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, recursive: true);
        }
    }

    /// <summary>
    /// Tries the path with and without its "map\" segment, then falls back to the first file
    /// anywhere in the region with the same name.
    /// </summary>
    private static string? FindInExtract(string extractDir, string objectHDB)
    {
        foreach (string rel in MapRegionReader.EntryCandidates(objectHDB))
        {
            string full = Path.Combine(extractDir, rel);
            if (File.Exists(full)) return full;
        }
        return Directory.EnumerateFiles(extractDir, Path.GetFileName(VfsPath.Normalize(objectHDB)),
                SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
    }
}
