using ShadowForge.Formats.HMB;
using ShadowForge.Formats.HDB;

namespace ShadowForge.Formats.GLTF;

public static class ModelExporter
{
    /// <summary>
    /// Exports HDB bytes to .glb or .gltf and returns the exporter warnings. Textures come
    /// from <paramref name="textureDir"/>, or from <paramref name="textures"/> (file name
    /// and DDS bytes), which are written to a temporary directory for the export and win
    /// when both are given.
    /// </summary>
    public static IReadOnlyList<string> Export(
        byte[] hdb,
        string outputPath,
        bool embed = true,
        IReadOnlyList<MotionClip>? clips = null,
        string? textureDir = null,
        IReadOnlyList<(string Name, byte[] DDS)>? textures = null,
        IReadOnlyList<string>? textureNames = null)
    {
        var model = ModelCooker.Bake(ModelReader.Read(hdb));
        string? tempDir = null;
        try
        {
            string? effectiveDir = textureDir;
            if (textures is { Count: > 0 })
            {
                tempDir = Path.Combine(Path.GetTempPath(), "sforge-tex-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                foreach (var (name, dds) in textures)
                    File.WriteAllBytes(Path.Combine(tempDir, name), dds);
                effectiveDir = tempDir;
            }
            return SceneExporter.Export(model, outputPath, effectiveDir, embed, clips, textureNames);
        }
        finally
        {
            if (tempDir is not null && Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
