using System.Text;
using ShadowForge.Formats.IPK;
using ShadowForge.GameData;

namespace ShadowForge.Tests.GameData;

public sealed class MapRegionReaderTests
{
    private static string NewGameRoot() => GameDataFixture.NewGameRoot("sf_mrr_");

    [Fact]
    public void Read_PackedRegion_WithMapPrefix()
    {
        string game = NewGameRoot();
        Directory.CreateDirectory(Path.Combine(game, "pack", "map", "ipk"));
        File.WriteAllBytes(Path.Combine(game, "pack", "map", "ipk", "bg01_00.ipk"),
            ArchiveWriter.Build(new[] { new ArchiveWriter.InputEntry(
                @"map\town\bg01\bg01_01fa.hdb", "TOWNHDB"u8.ToArray()) }));
        var r = new MapRegionReader(GameInstall.Locate(game), "bg01_00.ipk");
        Assert.True(r.Available);
        Assert.True(r.Exists(@"map\town\bg01\bg01_01fa.hdb"));
        Assert.Equal("TOWNHDB", Encoding.ASCII.GetString(
            r.Read(@"map\town\bg01\bg01_01fa.hdb")));
    }

    [Fact]
    public void Read_PackedRegion_WithoutMapPrefix()
    {
        string game = NewGameRoot();
        Directory.CreateDirectory(Path.Combine(game, "pack", "map", "ipk"));
        File.WriteAllBytes(Path.Combine(game, "pack", "map", "ipk", "bt02_01.ipk"),
            ArchiveWriter.Build(new[] { new ArchiveWriter.InputEntry(
                @"battle\bt02\bt02_01a.hdb", "BTLHDB"u8.ToArray()) }));
        var r = new MapRegionReader(GameInstall.Locate(game), "bt02_01.ipk");
        Assert.Equal("BTLHDB", Encoding.ASCII.GetString(
            r.Read(@"map\battle\bt02\bt02_01a.hdb")));
    }

    [Fact]
    public void Read_LooseRegion_WithAndWithoutMapPrefix()
    {
        string game = NewGameRoot();

        string keep = Path.Combine(game, "map", "ipk", "bg01_00", "map", "town", "bg01");
        Directory.CreateDirectory(keep);
        File.WriteAllText(Path.Combine(keep, "a.hdb"), "LOOSEKEEP");
        string drop = Path.Combine(game, "map", "ipk", "bt02_01", "battle", "bt02");
        Directory.CreateDirectory(drop);
        File.WriteAllText(Path.Combine(drop, "b.hdb"), "LOOSEDROP");

        var install = GameInstall.Locate(game);
        Assert.Equal("LOOSEKEEP", Encoding.ASCII.GetString(
            new MapRegionReader(install, "bg01_00.ipk").Read(@"map\town\bg01\a.hdb")));
        Assert.Equal("LOOSEDROP", Encoding.ASCII.GetString(
            new MapRegionReader(install, "bt02_01").Read(@"map\battle\bt02\b.hdb")));
    }

    [Fact]
    public void Read_MissingRegion_ThrowsWithContext()
    {
        string game = NewGameRoot();
        var r = new MapRegionReader(GameInstall.Locate(game), "zz99_00.ipk");
        Assert.False(r.Available);
        var ex = Assert.Throws<FileNotFoundException>(() => r.Read(@"map\town\zz99\x.hdb"));
        Assert.Contains("zz99_00", ex.Message);
    }
}
