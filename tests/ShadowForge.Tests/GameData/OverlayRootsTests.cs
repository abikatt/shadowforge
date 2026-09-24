using System.Text;
using ShadowForge.GameData;

namespace ShadowForge.Tests.GameData;

public sealed class OverlayRootsTests
{
    private static string NewTempDir() => GameDataFixture.NewTempDir("sf_ovl_");

    [Fact]
    public void ReadVfs_PrefersOverlayOverInstall()
    {
        string overlay = NewTempDir();
        string dest = Path.Combine(overlay, "chara", "ene", "em901", "em901_obj.hdb");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.WriteAllText(dest, "OVERLAY");

        var install = GameDataFixture.Install().WithOverlays(new[] { overlay });
        byte[] bytes = new GameFileSystem(install).ReadVfs(@"chara\ene\em901\em901_obj.hdb");

        Assert.Equal("OVERLAY", Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void ReadVfs_FallsThroughToInstallWhenOverlayLacksFile()
    {
        var install = GameDataFixture.Install().WithOverlays(new[] { NewTempDir() });
        byte[] bytes = new GameFileSystem(install).ReadVfs(@"chara\ene\em901\em901_obj.hdb");

        Assert.Equal("HDB", Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void ExistsVfs_TrueForOverlayOnlyFile()
    {
        string overlay = NewTempDir();
        string dest = Path.Combine(overlay, "database", "model", "chara", "ply", "model_pc11.mdl");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.WriteAllText(dest, "<ObjectData>");

        var install = GameDataFixture.Install().WithOverlays(new[] { overlay });
        var gfs = new GameFileSystem(install);

        Assert.True(gfs.ExistsVfs(@"database\model\chara\ply\model_pc11.mdl"));
        Assert.False(new GameFileSystem(GameDataFixture.Install())
            .ExistsVfs(@"database\model\chara\ply\model_pc11.mdl"));
    }

    [Fact]
    public void Overlays_AreSearchedInOrder()
    {
        string first = NewTempDir(), second = NewTempDir();
        foreach (var (dir, body) in new[] { (first, "FIRST"), (second, "SECOND") })
        {
            string p = Path.Combine(dir, "chara", "ene", "em901", "em901_obj.hdb");
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, body);
        }

        var install = GameDataFixture.Install().WithOverlays(new[] { first, second });
        byte[] bytes = new GameFileSystem(install).ReadVfs(@"chara\ene\em901\em901_obj.hdb");

        Assert.Equal("FIRST", Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void EnumerateVfs_IncludesOverlayFiles()
    {
        string overlay = NewTempDir();
        string dest = Path.Combine(overlay, "database", "model", "chara", "ply", "model_pc11.mdl");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.WriteAllText(dest, "<ObjectData>");

        var install = GameDataFixture.Install().WithOverlays(new[] { overlay });
        var found = new GameFileSystem(install)
            .EnumerateVfs(@"database\model\chara\ply", "model_*.mdl");

        Assert.Contains(@"database\model\chara\ply\model_pc11.mdl", found);
    }
}
