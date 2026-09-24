using ShadowForge.Formats.GLTF;

namespace ShadowForge.Tests.GLTF;

public sealed class TextureEmbedTests
{
    private const string HDBDir = "testdata/models/humanoid";
    private const string HDB = "testdata/models/humanoid/bs01_obj.hdb";

    [Fact]
    public void Export_TexturesInSameDir_EmbedsImages()
    {
        byte[] hdb = File.ReadAllBytes(HDB);
        string outPath = Path.Combine(Path.GetTempPath(), "sf_tex_" + Guid.NewGuid().ToString("N") + ".glb");
        try
        {
            ModelExporter.Export(hdb, outPath, embed: true, textureDir: HDBDir);
            var loaded = SharpGLTF.Schema2.ModelRoot.Load(outPath);
            Assert.NotEmpty(loaded.LogicalImages);
        }
        finally { if (File.Exists(outPath)) File.Delete(outPath); }
    }

    [Fact]
    public void Export_TexturesInNestedSubdir_EmbedsImages()
    {
        byte[] hdb = File.ReadAllBytes(HDB);
        string root = Path.Combine(Path.GetTempPath(), "sf_nest_" + Guid.NewGuid().ToString("N"));
        string nested = Path.Combine(root, "chara", "ene", "bs01");
        Directory.CreateDirectory(nested);
        foreach (var dds in Directory.EnumerateFiles(HDBDir, "*.dds"))
            File.Copy(dds, Path.Combine(nested, Path.GetFileName(dds)));
        string outPath = Path.Combine(Path.GetTempPath(), "sf_nest_" + Guid.NewGuid().ToString("N") + ".glb");
        try
        {
            ModelExporter.Export(hdb, outPath, embed: true, textureDir: root);
            var loaded = SharpGLTF.Schema2.ModelRoot.Load(outPath);
            Assert.NotEmpty(loaded.LogicalImages);
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
