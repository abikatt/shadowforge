using ShadowForge.GameData;
using ShadowForge.GameData.Entities;

namespace ShadowForge.Tests.GameData.Entities;

public sealed class EntityResolverTests
{
    private static EntityResolver MakeResolver() => new(GameDataFixture.Install());

    [Fact]
    public void Resolve_ById_ProducesSkeletonMotionAndVfsPaths()
    {
        var e = MakeResolver().Resolve("em901");
        Assert.Equal("em901", e.Id);
        Assert.Equal("ene", e.Class);
        Assert.Equal("em901", e.RigId);

        var skel = e.Files.Single(f => f.Role == FileRole.Skeleton);
        Assert.Equal(@"chara\ene\em901\em901_obj.hdb", skel.VfsPath);
        Assert.Equal(@"chara\ipk\em901\ene\em901\em901_obj.hdb", skel.LooseRelPath);
        Assert.Equal("em901.ipk", skel.IPKName);
        Assert.Equal(@"ene\em901\em901_obj.hdb", skel.IPKInnerPath);
        Assert.True(skel.Exists);

        Assert.Contains(e.Files, f => f.Role == FileRole.Motion && f.VfsPath.EndsWith("em901_mot.mpk"));
        Assert.Contains(e.Files, f => f.Role == FileRole.ModelDef);
    }

    [Fact]
    public void Resolve_DatabaseFiles_CarryDatabaseIPKPack()
    {
        var e = MakeResolver().Resolve("em901");

        var mdl = e.Files.Single(f => f.Role == FileRole.ModelDef);
        Assert.Equal("database.ipk", mdl.IPKName);
        Assert.Equal(@"model\chara\ene\model_em901.mdl", mdl.IPKInnerPath);

        var motscr = e.Files.Single(f => f.Role == FileRole.MotScr);
        Assert.Equal("database.ipk", motscr.IPKName);
        Assert.Equal(@"battle\motscr\motscr_em901.csv", motscr.IPKInnerPath);
    }

    [Fact]
    public void Resolve_OptionalMotCmd_IsMarkedAbsentNotThrown()
    {
        var e = MakeResolver().Resolve("em901");
        var mtc = e.Files.SingleOrDefault(f => f.Role == FileRole.MotCmd);
        if (mtc is not null) Assert.False(mtc.Exists);
        Assert.Contains(e.Files, f => f.Role == FileRole.MotScr && f.Exists);
    }

    [Fact]
    public void Resolve_SharedRig_UsesMDLPathRigNotOwnId()
    {
        var e = MakeResolver().Resolve("bs901");
        Assert.Equal("em901", e.RigId);
        var skel = e.Files.Single(f => f.Role == FileRole.Skeleton);
        Assert.Equal(@"chara\ene\em901\em901_obj.hdb", skel.VfsPath);
        Assert.True(skel.Exists);
    }

    [Fact]
    public void Resolve_ByMDLPath_ReadsThatFileAndModelDefExists()
    {
        string mdlPath = Path.Combine(GameDataFixture.Root, "database", "model", "chara", "ene", "model_em901.mdl");
        var e = MakeResolver().Resolve(mdlPath);
        Assert.Equal("em901", e.Id);
        Assert.True(e.Files.Single(f => f.Role == FileRole.ModelDef).Exists);
    }

    [Fact]
    public void Resolve_EmitsTextureOverrideAndOverlayFiles()
    {
        var e = GameDataFixture.ResolveWithMDL("em902",
            "\tPATH\t\t\t\t\"chara\\ene\\em902\\\"\n" +
            "\tOBJECT\t\t\t\t\"em902_obj.hdb\" \"em902_tex_a.csv\"\n" +
            "\tOBJECTOPT\t\t\t0 \"em902_efc_obj.hdb\"\n");
        Assert.Contains(e.Files, f => f.Role == FileRole.TextureOverride
            && f.VfsPath == @"chara\ene\em902\em902_tex_a.csv");
        Assert.Contains(e.Files, f => f.Role == FileRole.Overlay
            && f.VfsPath == @"chara\ene\em902\em902_efc_obj.hdb");
    }
}
