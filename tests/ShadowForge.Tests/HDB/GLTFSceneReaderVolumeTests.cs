using Microsoft.Extensions.Logging;
using ShadowForge.Formats.HDB.Import;
using ShadowForge.Tests.GLTF;
using ShadowForge.Tests.Logging;

namespace ShadowForge.Tests.HDB;

/// <summary>
/// The inverse of ExporterVolumeTests: a material tagged sfVolumeTexture carries a 2D
/// preview of a 3D fur texture. The importer keeps the binding by name and writes no
/// .dds for it, because the runtime loads the .36t that ships with the base rig.
/// </summary>
public sealed class GLTFSceneReaderVolumeTests
{
    private const string FurName = "voltex_pc05_fur_01";

    [Fact]
    public void VolumeMaterial_KeptAsNameOnlyEntry_WithWarning()
    {
        var (dir, _) = ExporterVolumeTests.ExportWithFur();
        try
        {
            var log = new CapturingLogger();
            var scene = GLTFSceneReader.Read(Path.Combine(dir, "model.glb"), log, formatOverride: null);

            int furIdx = scene.Textures.FindIndex(t => t.Name == FurName && !t.IsNormalMap);
            Assert.True(furIdx >= 0, "fur binding kept by name");
            Assert.Empty(scene.Textures[furIdx].DDSBytes);

            int bodyIdx = scene.Textures.FindIndex(t => t.Name == "body" && !t.IsNormalMap);
            Assert.True(bodyIdx >= 0);
            Assert.NotEmpty(scene.Textures[bodyIdx].DDSBytes);

            Assert.Contains(scene.Triangles, t => t.MaterialIndex == furIdx);
            Assert.Contains(scene.Triangles, t => t.MaterialIndex == bodyIdx);

            Assert.Contains(log.Entries, e =>
                e.Level == LogLevel.Warning
                && e.Message.Contains(FurName + ".36t")
                && e.Message.Contains("import-volume"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void VolumeMaterial_ImportWritesNoDDSForIt()
    {
        var (dir, _) = ExporterVolumeTests.ExportWithFur();
        try
        {
            string cooked = Path.Combine(dir, "cooked");
            Directory.CreateDirectory(cooked);
            var log = new CapturingLogger();
            ModelImporter.Import(Path.Combine(dir, "model.glb"), Path.Combine(cooked, "model.hdb"), log);

            Assert.True(File.Exists(Path.Combine(cooked, "body.dds")));
            Assert.False(File.Exists(Path.Combine(cooked, FurName + ".dds")));
        }
        finally { Directory.Delete(dir, true); }
    }
}
