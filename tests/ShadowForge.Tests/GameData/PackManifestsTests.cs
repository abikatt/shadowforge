using ShadowForge.GameData;

namespace ShadowForge.Tests.GameData;

public sealed class PackManifestsTests
{
    private static string NewRoot() => GameDataFixture.NewGameRoot("sf_pm_");

    private const string CharaCsv = "\"model_bs01.mdl\",\"bs01.ipk\"\n\"model_np346.mdl\",\"bs02.ipk\"\n";
    private const string TownCsv = "\"db_bg01_01.map\",\"bg01_00.ipk\"\n\"db_bg06_01.map\",\"bg01_00.ipk\"\n";
    private const string BattleCsv = "\"db_bt02_01.map\",\"bt02_01.ipk\"\n";

    [Fact]
    public void Load_ReadsCharaManifestFromPack()
    {
        string game = NewRoot();
        Directory.CreateDirectory(Path.Combine(game, "pack", "chara"));
        File.WriteAllText(Path.Combine(game, "pack", "chara", "pack_chr_model.txt"), CharaCsv);
        var m = PackManifests.Load(GameInstall.Locate(game));
        Assert.Equal("bs02.ipk", m.IPKForModel("model_np346.mdl"));
        Assert.Equal("bs01.ipk", m.IPKForModel("MODEL_BS01.MDL"));
        Assert.Null(m.IPKForModel("model_zz99.mdl"));
    }

    [Fact]
    public void Load_FallsBackToNecessityThenLooseForChara()
    {
        string game = NewRoot();
        Directory.CreateDirectory(Path.Combine(game, "!necessity", "pack", "chara"));
        File.WriteAllText(Path.Combine(game, "!necessity", "pack", "chara", "pack_chr_model.txt"), CharaCsv);
        Assert.Equal("bs01.ipk", PackManifests.Load(GameInstall.Locate(game)).IPKForModel("model_bs01.mdl"));

        string game2 = NewRoot();
        File.WriteAllText(Path.Combine(game2, "chara", "pack_chr_model.txt"), CharaCsv);
        Assert.Equal("bs01.ipk", PackManifests.Load(GameInstall.Locate(game2)).IPKForModel("model_bs01.mdl"));
    }

    [Fact]
    public void Load_ReadsMapManifestsWithCategories()
    {
        string game = NewRoot();
        Directory.CreateDirectory(Path.Combine(game, "pack", "map"));
        File.WriteAllText(Path.Combine(game, "pack", "map", "pack_map_town.txt"), TownCsv);
        File.WriteAllText(Path.Combine(game, "pack", "map", "pack_map_battle.txt"), BattleCsv);
        var m = PackManifests.Load(GameInstall.Locate(game));
        Assert.Equal("bg01_00.ipk", m.MapToIPK["db_bg01_01.map"]);
        Assert.Equal("town", m.MapCategory["db_bg01_01.map"]);
        Assert.Equal("battle", m.MapCategory["db_bt02_01.map"]);
        Assert.Equal(3, m.MapToIPK.Count);
    }

    [Fact]
    public void IPKForMap_NormalizesStageIds()
    {
        string game = NewRoot();
        Directory.CreateDirectory(Path.Combine(game, "pack", "map"));
        File.WriteAllText(Path.Combine(game, "pack", "map", "pack_map_town.txt"), TownCsv);
        var m = PackManifests.Load(GameInstall.Locate(game));
        Assert.Equal("bg01_00.ipk", m.IPKForMap("bg01_01"));
        Assert.Equal("bg01_00.ipk", m.IPKForMap("db_bg01_01"));
        Assert.Equal("bg01_00.ipk", m.IPKForMap("db_bg01_01.map"));
        Assert.Null(m.IPKForMap("zz99_99"));
    }

    [Fact]
    public void MapDefVfsCandidates_LeadsWithCategoryDirectory()
    {
        string game = NewRoot();
        Directory.CreateDirectory(Path.Combine(game, "pack", "map"));
        File.WriteAllText(Path.Combine(game, "pack", "map", "pack_map_town.txt"), TownCsv);
        File.WriteAllText(Path.Combine(game, "pack", "map", "pack_map_battle.txt"), BattleCsv);
        File.WriteAllText(Path.Combine(game, "pack", "map", "pack_map_cube.txt"),
            "\"db_cb01_01.map\",\"cb01_00.ipk\"\n");
        var m = PackManifests.Load(GameInstall.Locate(game));

        Assert.Equal(new[]
        {
            @"database\map\battle\db_bt02_01.map",
            @"database\map\db_bt02_01.map",
            @"database\map\event\db_bt02_01.map",
            @"database\map\world\db_bt02_01.map",
        }, m.MapDefVfsCandidates("bt02_01"));

        Assert.Equal(@"database\map\db_bg01_01.map", m.MapDefVfsCandidates("bg01_01")[0]);
        Assert.Equal(@"database\map\world\db_cb01_01.map", m.MapDefVfsCandidates("cb01_01")[0]);

        Assert.Equal(@"database\map\db_zz99_99.map", m.MapDefVfsCandidates("zz99_99")[0]);
        Assert.Equal(4, m.MapDefVfsCandidates("zz99_99").Count);
    }

    [Fact]
    public void Load_NothingPresent_ReturnsEmptyMaps()
    {
        string game = NewRoot();
        var m = PackManifests.Load(GameInstall.Locate(game));
        Assert.Empty(m.CharaModelToIPK);
        Assert.Empty(m.MapToIPK);
    }

    [Fact]
    public void ParseCsv_RealManifestRows_Parse()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "GameData/Fixtures/manifests");
        var chara = PackManifests.ParseCsv(File.ReadAllText(Path.Combine(dir, "pack_chr_model.txt")));
        Assert.Equal("bs02.ipk", chara["model_np346.mdl"]);
        Assert.Equal(10, chara.Count);
        var town = PackManifests.ParseCsv(File.ReadAllText(Path.Combine(dir, "pack_map_town.txt")));
        Assert.Equal("bg01_00.ipk", town["db_bg01_01.map"]);
    }
}
