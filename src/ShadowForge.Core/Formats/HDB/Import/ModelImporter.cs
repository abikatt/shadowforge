using Microsoft.Extensions.Logging;
using ShadowForge.Formats.DDS;

namespace ShadowForge.Formats.HDB.Import;

/// <summary>
/// glTF to HDB importer. Reads the scene, splits it into palette-limited draw batches,
/// writes the HDB, then runs ModelValidator over the bytes and throws before writing
/// anything when the result would not load.
/// </summary>
public static class ModelImporter
{
    public static void Import(string gltfPath, string hdbOut, ILogger log, GraphicFormat? formatOverride = null)
    {
        var scene = GLTFSceneReader.Read(gltfPath, log, formatOverride);
        var batches = BatchBuilder.Build(scene);

        int stripIndices = batches.Sum(b => b.StripIndices.Count);
        log.LogInformation(
            "{Batches} draw batch(es), {Tris} triangles -> {Indices} strip indices, {Verts} packed vertices ({Mode}).",
            batches.Count, scene.Triangles.Count, stripIndices,
            batches.Sum(b => b.Vertices.Count), scene.Skinned ? "skinned 100B" : "rigid 48B");

        byte[] hdb = FileBuilder.Build(scene, batches);

        var validation = ModelValidator.Run(hdb);
        foreach (var f in validation.Findings)
        {
            if (f.Level == ModelValidator.Level.Error) log.LogError("validator: {Msg}", f.Message);
            else if (f.Level == ModelValidator.Level.Warning) log.LogWarning("validator: {Msg}", f.Message);
        }
        if (validation.HasErrors)
            throw new InvalidDataException(
                "Emitted HDB would not load; refusing to write. See validator errors above.");

        string outDir = Path.GetDirectoryName(Path.GetFullPath(hdbOut)) ?? ".";
        foreach (var tex in scene.Textures)
        {
            if (tex.DDSBytes.Length == 0)
            {
                log.LogInformation(
                    "Skipped writing {Name}.dds (name-only texture-table entry; "
                    + "the real file must ship alongside the model).", tex.Name);
                continue;
            }
            string ddsPath = Path.Combine(outDir, tex.Name + ".dds");
            File.WriteAllBytes(ddsPath, tex.DDSBytes);
            log.LogInformation("Wrote {Path}", ddsPath);
        }

        File.WriteAllBytes(hdbOut, hdb);
        log.LogInformation("Wrote {Path} ({Size} bytes, validator clean).", hdbOut, hdb.Length);
    }
}
