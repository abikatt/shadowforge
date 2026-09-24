using ShadowForge.GameData;
using ShadowForge.GameData.Entities;
using ShadowForge.GameData.Mods;

namespace ShadowForge.Tests.GameData.Mods;

public sealed class ModOverrideTests
{
    private static ResolvedEntity Resolve(string id) =>
        new EntityResolver(GameDataFixture.Install()).Resolve(id);

    [Fact]
    public void OverridePath_UsesVfsForRigAssets_AndLooseForDatabaseFiles()
    {
        var e = Resolve("em901");
        var skel = e.Files.Single(f => f.Role == FileRole.Skeleton);
        var mdl = e.Files.Single(f => f.Role == FileRole.ModelDef);
        Assert.Equal(@"chara\ene\em901\em901_obj.hdb", ModOverride.OverridePath(skel));
        Assert.Equal(@"database\model\chara\ene\model_em901.mdl", ModOverride.OverridePath(mdl));
    }

    [Fact]
    public void OverridePathFor_RoutesByFilename()
    {
        var e = Resolve("em901");
        Assert.Equal(@"chara\ene\em901\em901_obj.hdb", ModOverride.OverridePathFor(e, "em901_obj.hdb"));
        Assert.Equal(@"chara\ene\em901\em901_01.dds",  ModOverride.OverridePathFor(e, "em901_01.dds"));
        Assert.Equal(@"database\model\chara\ene\model_em901.mdl", ModOverride.OverridePathFor(e, "model_em901.mdl"));
        Assert.Equal(@"database\battle\motscr\motscr_em901.csv",  ModOverride.OverridePathFor(e, "motscr_em901.csv"));
        Assert.Equal(@"database\motcmd\ene\mtc_em901.csv",        ModOverride.OverridePathFor(e, "mtc_em901.csv"));

        string editedFile = Path.Combine(Path.GetTempPath(), "edit", "em901_02.dds");
        Assert.Equal(@"chara\ene\em901\em901_02.dds", ModOverride.OverridePathFor(e, editedFile));
    }

    [Fact]
    public void OverridePathFor_SharedRig_UsesRigClassAndRigId()
    {
        var e = Resolve("bs901");
        Assert.Equal("em901", e.RigId);
        Assert.Equal(@"chara\ene\em901\em901_obj.hdb", ModOverride.OverridePathFor(e, "em901_obj.hdb"));
        Assert.Equal(@"database\model\chara\ene\model_bs901.mdl", ModOverride.OverridePathFor(e, "model_bs901.mdl"));
    }

    [Fact]
    public void OverridePathFor_RejectsARigPathThatClimbsOutOfTheCharaTree()
    {
        var e = GameDataFixture.ResolveWithMDL("em905",
            "\tPATH\t\t\t\t\"chara\\..\\..\\em905\\\"\n" +
            "\tOBJECT\t\t\t\t\"em905_obj.hdb\"\n");

        var ex = Assert.Throws<InvalidOperationException>(
            () => ModOverride.OverridePathFor(e, "em905_obj.hdb"));

        Assert.Contains("the rig class in the PATH of 'em905'", ex.Message);
        Assert.Contains("walks outside the mod folder", ex.Message);
    }

    [Fact]
    public void RoleFor_ClassifiesKnownFilenames()
    {
        var e = Resolve("em901");
        Assert.Equal(FileRole.ModelDef, ModOverride.RoleFor(e, "model_em901.mdl"));
        Assert.Equal(FileRole.MotScr,   ModOverride.RoleFor(e, "motscr_em901.csv"));
        Assert.Equal(FileRole.MotCmd,   ModOverride.RoleFor(e, "mtc_em901.csv"));
        Assert.Equal(FileRole.Skeleton, ModOverride.RoleFor(e, "em901_obj.hdb"));
        Assert.Equal(FileRole.Motion,   ModOverride.RoleFor(e, "em901_mot.mpk"));
        Assert.Equal(FileRole.Texture,  ModOverride.RoleFor(e, "em901_01.dds"));
        Assert.Equal(FileRole.Other,    ModOverride.RoleFor(e, "em901_face_normal.csv"));
    }

    [Fact]
    public void RoleFor_ClassifiesOverlayCsvAndShellTexture()
    {
        var e = GameDataFixture.ResolveWithMDL("em903",
            "\tPATH\t\t\t\t\"chara\\ene\\em903\\\"\n" +
            "\tOBJECT\t\t\t\t\"em903_obj.hdb\"\t\"em903_tex_a.csv\"\n" +
            "\tOBJECTOPT\t\t\t0 \"em903_efc_eye_obj.hdb\"\n");
        Assert.Equal(FileRole.Skeleton, ModOverride.RoleFor(e, "em903_obj.hdb"));
        Assert.Equal(FileRole.Overlay, ModOverride.RoleFor(e, "em903_efc_eye_obj.hdb"));
        Assert.Equal(FileRole.TextureOverride, ModOverride.RoleFor(e, "em903_tex_a.csv"));
        Assert.Equal(FileRole.ShellTexture, ModOverride.RoleFor(e, "voltex_em903_01.36t"));
        Assert.Equal(FileRole.Texture, ModOverride.RoleFor(e, "em903_01.dds"));
    }
}
