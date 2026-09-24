using System.Text.Json;
using ShadowForge.Formats.GLTF;
using ShadowForge.Formats.HMB;
using ShadowForge.Formats.MDL;
using ShadowForge.Manifests;

namespace ShadowForge.GameData.Entities;

public sealed record EntityExportResult(
    string Id, string ModelPath, string ManifestPath, int ClipCount, IReadOnlyList<string> Warnings);

/// <summary>
/// Exports a chara entity for editing: "{id}.glb" (or "{id}.gltf" with external textures)
/// and "{id}.sfmod.json". The rig is extracted to a temp folder that is removed afterwards.
/// Textures named by the model def's override CSV but missing from the rig are warnings.
/// </summary>
public sealed class EntityExporter
{
    private readonly GameInstall _install;

    public EntityExporter(GameInstall install) => _install = install;

    public EntityExportResult Export(string idOrPath, string outDir, bool embed = true,
        bool includeAnimations = true, bool includeTextures = true, Action<string>? progress = null)
    {
        Directory.CreateDirectory(outDir);
        var entity = new EntityResolver(_install).Resolve(idOrPath);

        string rigDir = Path.Combine(Path.GetTempPath(), "sforge", "rig-" + Guid.NewGuid().ToString("N"));
        try
        {
            progress?.Invoke($"materializing rig for {entity.Id}...");
            var rig = new EntityAssets(_install).MaterializeRig(idOrPath, rigDir);
            if (rig.Skeleton is null)
                throw new FileNotFoundException($"No skeleton .hdb resolved for {entity.Id}.");
            byte[] hdb = File.ReadAllBytes(rig.Skeleton);

            List<MotionClip>? clips = null;
            if (includeAnimations && rig.Motion is not null)
            {
                progress?.Invoke("reading animation clips...");
                clips = MotPack.ReadAll(rig.Motion, out _);
            }

            IReadOnlyList<string>? texNames = null;
            var warnings = new List<string>();
            if (rig.TextureOverrideCsv is not null)
            {
                texNames = TexCsvFile.ReadFile(rig.TextureOverrideCsv).DDSNames;
                foreach (string name in texNames)
                {
                    if (TextureResolver.FindDDS(name, rig.Dir) is null && TextureResolver.FindVolume(name, rig.Dir) is null)
                        warnings.Add(
                            $"override texture '{name}' (from {Path.GetFileName(rig.TextureOverrideCsv)}) not found in rig workspace");
                }
            }

            string modelPath = Path.Combine(outDir, entity.Id + (embed ? ".glb" : ".gltf"));
            int clipCount = clips?.Count ?? 0;
            progress?.Invoke($"writing {Path.GetFileName(modelPath)} ({clipCount} clip(s))...");
            warnings.InsertRange(0, ModelExporter.Export(hdb, modelPath, embed, clips,
                textureDir: includeTextures ? rig.Dir : null, textureNames: includeTextures ? texNames : null));

            var manifest = ManifestBuilder.ForExport(entity, _install,
                rig.Textures.Concat(rig.ShellTextures).ToList(), Path.GetFileName(modelPath), clipCount);
            string manifestPath = Path.Combine(outDir, entity.Id + ".sfmod.json");
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, ManifestJson.Default.EntityManifest));

            return new EntityExportResult(entity.Id, modelPath, manifestPath, clipCount, warnings);
        }
        finally
        {
            if (Directory.Exists(rigDir)) Directory.Delete(rigDir, recursive: true);
        }
    }
}
