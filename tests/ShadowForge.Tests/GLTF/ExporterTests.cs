using ShadowForge.Formats.GLTF;
using ShadowForge.Tests.HDB;

namespace ShadowForge.Tests.GLTF;

public sealed class ExporterTests
{
    [Fact]
    public void Export_ProducesValidGlb()
    {
        var model = SampleModel.Cooked();
        var outputPath = Path.GetTempFileName() + ".glb";

        try
        {
            SceneExporter.Export(model, outputPath);
            Assert.True(File.Exists(outputPath));

            var bytes = File.ReadAllBytes(outputPath);
            Assert.True(bytes.Length > 100);
            Assert.Equal((byte)'g', bytes[0]);
            Assert.Equal((byte)'l', bytes[1]);
            Assert.Equal((byte)'T', bytes[2]);
            Assert.Equal((byte)'F', bytes[3]);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public void Export_GlbIsLoadable()
    {
        var model = SampleModel.Cooked();
        var outputPath = Path.GetTempFileName() + ".glb";

        try
        {
            SceneExporter.Export(model, outputPath);
            var loaded = SharpGLTF.Schema2.ModelRoot.Load(outputPath);
            Assert.NotNull(loaded);
            Assert.NotEmpty(loaded.LogicalMeshes);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public void Export_TextureNameOverride_RenamesMaterialsOrdinally()
    {
        var model = SampleModel.Cooked();
        string outPath = Path.Combine(Path.GetTempPath(), "sf_ovr_" + Guid.NewGuid().ToString("N") + ".glb");
        var names = Enumerable.Range(0, model.TextureCount)
            .Select(i => "override_" + i).ToList();
        SceneExporter.Export(model, outPath, textureDir: null, embed: true,
            clips: null, textureNames: names);
        var gltf = SharpGLTF.Schema2.ModelRoot.Load(outPath);
        Assert.Contains(gltf.LogicalMaterials, m => m.Name == "override_0");
    }

    [Fact]
    public void Export_NullTextureDir_ProducesFlatMaterials_AndNoImages()
    {
        var model = SampleModel.Cooked();
        string outPath = Path.Combine(Path.GetTempPath(), "sf_notex_" + Guid.NewGuid().ToString("N") + ".glb");
        SceneExporter.Export(model, outPath, textureDir: null, embed: true, clips: null);
        var gltf = SharpGLTF.Schema2.ModelRoot.Load(outPath);
        Assert.Empty(gltf.LogicalImages);
        Assert.Empty(gltf.LogicalAnimations);
    }

    [Fact]
    public void ExportMany_EmitsNamedNodesUnderNestedGroups()
    {
        var a = SampleModel.Cooked();
        var b = SampleModel.Cooked();

        foreach (var g in a.MeshGroups.Concat(b.MeshGroups))
        {
            g.Stage1TexIndex = -1;
            g.Stage2TexIndex = -1;
        }
        string outPath = Path.Combine(Path.GetTempPath(), "sf_many_" + Guid.NewGuid().ToString("N") + ".glb");
        SceneExporter.ExportMany(new[]
        {
            new RigidPlacement("bg01_01fa", a, "AREA_0/PRI_0"),
            new RigidPlacement("bg01_sky_a", b, "AREA_0/PRI_-8"),
        }, outPath);
        var gltf = SharpGLTF.Schema2.ModelRoot.Load(outPath);
        var names = gltf.LogicalNodes.Select(n => n.Name).ToList();
        Assert.Contains("AREA_0", names);
        Assert.Contains("PRI_0", names);
        Assert.Contains("PRI_-8", names);
        Assert.Contains("bg01_01fa", names);
        Assert.Contains("bg01_sky_a", names);
    }
}
