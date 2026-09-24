using System.Text;
using ShadowForge.Formats.IPK;
using ShadowForge.GameData;
using ShadowForge.GameData.Entities;

namespace ShadowForge.Tests.GameData.Entities;

public sealed class EntityAssetsTests
{
    private static string TempDir() => GameDataFixture.TempPath("sf_assets_");

    [Fact]
    public void MaterializeFile_Skeleton_WritesHDBFromRig()
    {
        var assets = new EntityAssets(GameDataFixture.Install());
        string path = assets.MaterializeFile("em901", FileRole.Skeleton, TempDir());
        Assert.Equal("em901_obj.hdb", Path.GetFileName(path));
        Assert.Equal("HDB", File.ReadAllText(path));
    }

    [Fact]
    public void MaterializeFile_ModelDef_WritesMDLFromDatabase()
    {
        var assets = new EntityAssets(GameDataFixture.Install());
        string path = assets.MaterializeFile("em901", FileRole.ModelDef, TempDir());
        Assert.Equal("model_em901.mdl", Path.GetFileName(path));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void MaterializeRig_Loose_LocatesSkeletonAndMotionAsSiblings()
    {
        var rig = new EntityAssets(GameDataFixture.Install()).MaterializeRig("em901", TempDir());
        Assert.NotNull(rig.Skeleton);
        Assert.NotNull(rig.Motion);
        Assert.Equal("em901_obj.hdb", Path.GetFileName(rig.Skeleton!));
        Assert.Equal("em901_mot.mpk", Path.GetFileName(rig.Motion!));
        Assert.Equal(Path.GetDirectoryName(rig.Skeleton), Path.GetDirectoryName(rig.Motion));
    }

    [Fact]
    public void MaterializeRig_FromIPK_ExtractsTextures()
    {
        string game = GameDataFixture.NewGameRoot("sf_rig_");
        Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(game)!, "mods"));
        Directory.CreateDirectory(Path.Combine(game, "database", "model", "chara", "ene"));
        Directory.CreateDirectory(Path.Combine(game, "pack", "chara", "ipk"));

        File.Copy(
            Path.Combine(GameDataFixture.Root, "database", "model", "chara", "ene", "model_em901.mdl"),
            Path.Combine(game, "database", "model", "chara", "ene", "model_em901.mdl"));

        var ipk = ArchiveWriter.Build(new[]
        {
            new ArchiveWriter.InputEntry(@"ene\em901\em901_obj.hdb", "HDB"u8.ToArray()),
            new ArchiveWriter.InputEntry(@"ene\em901\em901_mot.mpk", "MPK"u8.ToArray()),
            new ArchiveWriter.InputEntry(@"ene\em901\em901_01.dds", "DDS1"u8.ToArray()),
            new ArchiveWriter.InputEntry(@"ene\em901\em901_02.dds", "DDS2"u8.ToArray()),
        });
        File.WriteAllBytes(Path.Combine(game, "pack", "chara", "ipk", "em901.ipk"), ipk);

        var rig = new EntityAssets(GameInstall.Locate(game)).MaterializeRig("em901", TempDir());
        Assert.NotNull(rig.Skeleton);
        Assert.NotNull(rig.Motion);
        Assert.Equal(2, rig.Textures.Count);
        Assert.All(rig.Textures, t => Assert.True(File.Exists(t)));
    }

    [Fact]
    public void MaterializeRig_Surfaces36tAndOverrideCsv()
    {
        var rig = new EntityAssets(GameDataFixture.Install()).MaterializeRig("em901", TempDir());
        Assert.Contains(rig.ShellTextures, p => p.EndsWith("voltex_em901.36t"));
        Assert.NotNull(rig.TextureOverrideCsv);
        Assert.EndsWith("em901_tex_a.csv", rig.TextureOverrideCsv);
    }

    [Fact]
    public void MaterializeRig_UsesManifestIPKWhenModelMapsElsewhere()
    {
        string game = Path.Combine(GameDataFixture.TempPath("sf_mrig_"), "game");
        Directory.CreateDirectory(Path.Combine(game, "database", "model", "chara", "ene"));
        File.WriteAllText(
            Path.Combine(game, "database", "model", "chara", "ene", "model_em901.mdl"),
            "PATH\t\"chara\\ene\\em901\\\"\nOBJECT\t\"em901_obj.hdb\"\n");
        Directory.CreateDirectory(Path.Combine(game, "pack", "chara", "ipk"));
        File.WriteAllText(Path.Combine(game, "pack", "chara", "pack_chr_model.txt"),
            "\"model_em901.mdl\",\"shared.ipk\"\n");
        File.WriteAllBytes(Path.Combine(game, "pack", "chara", "ipk", "shared.ipk"),
            ArchiveWriter.Build(new[]
            {
                new ArchiveWriter.InputEntry(@"ene\em901\em901_obj.hdb", "HDBBYTES"u8.ToArray()),
            }));

        var rig = new EntityAssets(GameInstall.Locate(game)).MaterializeRig("em901", TempDir());
        Assert.NotNull(rig.Skeleton);
        Assert.Equal("HDBBYTES", Encoding.ASCII.GetString(File.ReadAllBytes(rig.Skeleton!)));
    }
}
