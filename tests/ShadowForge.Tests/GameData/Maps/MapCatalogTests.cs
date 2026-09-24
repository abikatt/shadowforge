using ShadowForge.Formats.IPK;
using ShadowForge.GameData;
using ShadowForge.GameData.Maps;

namespace ShadowForge.Tests.GameData.Maps;

public sealed class MapCatalogTests
{
    [Fact]
    public void List_JoinsManifestAvailabilityAndModelCount()
    {
        string game = GameDataFixture.NewGameRoot("sf_mcat_");
        Directory.CreateDirectory(Path.Combine(game, "pack", "map", "ipk"));
        File.WriteAllText(Path.Combine(game, "pack", "map", "pack_map_town.txt"),
            "\"db_bg01_01.map\",\"bg01_00.ipk\"\n\"db_bg99_01.map\",\"bg99_00.ipk\"\n");

        File.WriteAllBytes(Path.Combine(game, "pack", "map", "ipk", "bg01_00.ipk"),
            ArchiveWriter.Build(new[] { new ArchiveWriter.InputEntry(
                @"map\town\bg01\a.hdb", "X"u8.ToArray()) }));

        Directory.CreateDirectory(Path.Combine(game, "database", "map"));
        File.WriteAllText(Path.Combine(game, "database", "map", "db_bg01_01.map"),
            "<MODEL NAME=\"a.hdb\" AREA=0 PRI=0.000000>\n\tOBJECT\t\"map\\town\\bg01\\a.hdb\"\n</MODEL>\n" +
            "<MODEL NAME=\"b.hdb\" AREA=0 PRI=0.000000>\n\tOBJECT\t\"map\\town\\bg01\\b.hdb\"\n</MODEL>\n");

        var result = new MapCatalog(GameInstall.Locate(game)).List();

        Assert.Equal(2, result.Stages.Count);
        var bg01 = result.Stages.Single(s => s.StageId == "bg01_01");
        Assert.Equal("town", bg01.Category);
        Assert.Equal("bg01_00.ipk", bg01.RegionIPK);
        Assert.True(bg01.RegionAvailable);
        Assert.Equal(2, bg01.ModelCount);

        var bg99 = result.Stages.Single(s => s.StageId == "bg99_01");
        Assert.False(bg99.RegionAvailable);
        Assert.Equal(0, bg99.ModelCount);
        Assert.Contains(result.Warnings, w => w.Contains("db_bg99_01.map"));
    }

    [Fact]
    public void List_ReadsCategoryShelvedStageDefs()
    {
        string game = GameDataFixture.NewGameRoot("sf_mcat3_");
        Directory.CreateDirectory(Path.Combine(game, "pack", "map"));
        File.WriteAllText(Path.Combine(game, "pack", "map", "pack_map_battle.txt"),
            "\"db_bt20_02.map\",\"bt20_00.ipk\"\n");
        Directory.CreateDirectory(Path.Combine(game, "database", "map", "battle"));
        File.WriteAllText(Path.Combine(game, "database", "map", "battle", "db_bt20_02.map"),
            "<MODEL NAME=\"a.hdb\" AREA=0 PRI=0.000000>\n\tOBJECT\t\"map\\battle\\bt20\\a.hdb\"\n</MODEL>\n");

        var result = new MapCatalog(GameInstall.Locate(game)).List();

        var bt20 = result.Stages.Single(s => s.StageId == "bt20_02");
        Assert.Equal(1, bt20.ModelCount);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("db_bt20_02.map"));
    }

    [Fact]
    public void List_WarnsForUnmanifestedStageDefs()
    {
        string game = GameDataFixture.NewGameRoot("sf_mcat2_");
        Directory.CreateDirectory(Path.Combine(game, "pack", "map"));
        File.WriteAllText(Path.Combine(game, "pack", "map", "pack_map_town.txt"),
            "\"db_bg01_01.map\",\"bg01_00.ipk\"\n");
        Directory.CreateDirectory(Path.Combine(game, "database", "map"));
        File.WriteAllText(Path.Combine(game, "database", "map", "db_bg01_01.map"), "");
        File.WriteAllText(Path.Combine(game, "database", "map", "db_bg77_01.map"), "");

        var result = new MapCatalog(GameInstall.Locate(game)).List();

        Assert.Contains(result.Warnings, w => w.Contains("db_bg77_01.map"));
        Assert.DoesNotContain(result.Stages, s => s.StageId == "bg77_01");
    }
}
