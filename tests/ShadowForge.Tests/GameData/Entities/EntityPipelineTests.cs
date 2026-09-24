using ShadowForge.Formats.HMB;
using ShadowForge.GameData;
using ShadowForge.GameData.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace ShadowForge.Tests.GameData.Entities;

/// <summary>
/// Export then cook on a loose install whose pc11 rig is the bs01 test model.
/// </summary>
public sealed class EntityPipelineTests
{
    private const string HDB = "testdata/models/humanoid/bs01_obj.hdb";
    private const string MPK = "testdata/models/humanoid/bs01_mot.mpk";

    private static GameInstall LooseInstall()
    {
        string game = GameDataFixture.NewGameRoot("sf_pipeline_");
        string mdlDir = Path.Combine(game, "database", "model", "chara", "ply");
        Directory.CreateDirectory(mdlDir);
        File.WriteAllText(Path.Combine(mdlDir, "model_pc11.mdl"),
            "PATH\t\"chara\\ply\\pc11\\\"\nOBJECT\t\"pc11_obj.hdb\"\nMOTPACK\t\"pc11_mot.mpk\"\n");
        string rigDir = Path.Combine(game, "chara", "ply", "pc11");
        Directory.CreateDirectory(rigDir);
        File.Copy(HDB, Path.Combine(rigDir, "pc11_obj.hdb"));
        File.Copy(MPK, Path.Combine(rigDir, "pc11_mot.mpk"));
        return GameInstall.Locate(game);
    }

    [Fact]
    public void Export_WritesModelManifestAndEveryClip()
    {
        string outDir = GameDataFixture.NewTempDir("sf_export_");
        var result = new EntityExporter(LooseInstall()).Export("pc11", outDir, includeTextures: false);

        Assert.Equal("pc11", result.Id);
        Assert.Equal(Path.Combine(outDir, "pc11.glb"), result.ModelPath);
        Assert.True(File.Exists(result.ModelPath));
        Assert.True(File.Exists(result.ManifestPath));
        Assert.Equal(MotPack.ReadAll(MPK, out _).Count, result.ClipCount);
    }

    [Fact]
    public void Export_Gltf_WhenNotEmbedded()
    {
        string outDir = GameDataFixture.NewTempDir("sf_export_");
        var result = new EntityExporter(LooseInstall())
            .Export("pc11", outDir, embed: false, includeAnimations: false, includeTextures: false);

        Assert.Equal(".gltf", Path.GetExtension(result.ModelPath));
        Assert.Equal(0, result.ClipCount);
    }

    [Fact]
    public void Cook_ExportedModel_WritesHDBAndTemplatedMotionPack()
    {
        var install = LooseInstall();
        string fromDir = GameDataFixture.NewTempDir("sf_cookfrom_");
        new EntityExporter(install).Export("pc11", fromDir, includeTextures: false);

        var entity = new EntityResolver(install).Resolve("pc11");
        string model = EntityCooker.FindEditedModel(fromDir, "pc11");
        string cooked = Path.Combine(fromDir, "cooked");
        var result = new EntityCooker(install).Cook(entity, model, cooked, NullLogger.Instance);

        string hdb = Path.Combine(cooked, "pc11_obj.hdb");
        string mpk = Path.Combine(cooked, "pc11_mot.mpk");
        Assert.Contains(hdb, result.Outputs);
        Assert.Contains(mpk, result.Outputs);
        Assert.True(File.Exists(hdb));
        Assert.Equal(MotPack.ReadAll(MPK, out _).Count, MotPack.ReadAll(mpk, out _).Count);
    }

    [Fact]
    public void FindEditedModel_NoModel_Throws()
    {
        string dir = GameDataFixture.NewTempDir("sf_empty_");
        Assert.Throws<FileNotFoundException>(() => EntityCooker.FindEditedModel(dir, "pc11"));
    }
}
