using System.Text.Json;
using ShadowForge.Formats.IPK;
using ShadowForge.GameData;
using ShadowForge.GameData.Maps;
using ShadowForge.Manifests;

namespace ShadowForge.Tests.GameData.Maps;

public sealed class MapExporterTests
{
    [Fact]
    public void Export_WritesGlbAndCompleteManifest()
    {
        string game = GameDataFixture.NewGameRoot("sf_mexp_");
        Directory.CreateDirectory(Path.Combine(game, "pack", "map", "ipk"));
        File.WriteAllText(Path.Combine(game, "pack", "map", "pack_map_town.txt"),
            "\"db_zz01_01.map\",\"zz01_00.ipk\"\n");

        byte[] sphereHDB = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "testdata/models/simple/sphere.hdb"));
        File.WriteAllBytes(Path.Combine(game, "pack", "map", "ipk", "zz01_00.ipk"),
            ArchiveWriter.Build(new[] { new ArchiveWriter.InputEntry(
                @"map\town\zz01\zz01_a.hdb", sphereHDB) }));

        Directory.CreateDirectory(Path.Combine(game, "database", "map"));
        File.WriteAllText(Path.Combine(game, "database", "map", "db_zz01_01.map"),
            "<MODEL NAME=\"zz01_a.hdb\" AREA=0 PRI=0.000000>\n" +
            "\tOBJECT\t\"map\\town\\zz01\\zz01_a.hdb\"\n" +
            "</MODEL>\n" +
            "<MODEL NAME=\"missing.hdb\" AREA=0 PRI=1.000000>\n" +
            "\tOBJECT\t\"map\\town\\zz01\\missing.hdb\"\n" +
            "</MODEL>\n" +
            "PARTS\tEFC\t\"map\\town\\zz01\\zz01.efc\"\n");

        var install = GameInstall.Locate(game);
        string outDir = Path.Combine(Path.GetDirectoryName(game)!, "out");

        var result = new MapExporter(install).Export("zz01_01", outDir);

        Assert.True(File.Exists(result.GlbPath));
        Assert.True(File.Exists(result.ManifestPath));
        var man = JsonSerializer.Deserialize(
            File.ReadAllText(result.ManifestPath),
            ManifestJson.Default.MapManifest)!;
        Assert.Equal("zz01_01", man.StageId);
        Assert.Equal(2, man.Models.Count);
        Assert.Contains(man.Models, m => m.Exported);
        Assert.Contains(man.Models, m => !m.Exported);
        Assert.Contains(man.Skipped, s => s.Reason == "hdb-not-found-in-region");
        Assert.Contains(man.Skipped, s => s.Reason == "sidecar-not-imported-v1");
    }

    [Fact]
    public void Export_ReadsCategoryShelvedStageDef_AndReportsProgress()
    {
        string game = GameDataFixture.NewGameRoot("sf_mexp2_");
        Directory.CreateDirectory(Path.Combine(game, "pack", "map", "ipk"));
        File.WriteAllText(Path.Combine(game, "pack", "map", "pack_map_battle.txt"),
            "\"db_bt20_02.map\",\"bt20_00.ipk\"\n");

        byte[] sphereHDB = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "testdata/models/simple/sphere.hdb"));
        File.WriteAllBytes(Path.Combine(game, "pack", "map", "ipk", "bt20_00.ipk"),
            ArchiveWriter.Build(new[] { new ArchiveWriter.InputEntry(
                @"map\battle\bt20\bt20_a.hdb", sphereHDB) }));

        Directory.CreateDirectory(Path.Combine(game, "database", "map", "battle"));
        File.WriteAllText(Path.Combine(game, "database", "map", "battle", "db_bt20_02.map"),
            "<MODEL NAME=\"bt20_a.hdb\" AREA=0 PRI=0.000000>\n" +
            "\tOBJECT\t\"map\\battle\\bt20\\bt20_a.hdb\"\n" +
            "</MODEL>\n");

        var install = GameInstall.Locate(game);
        string outDir = Path.Combine(Path.GetDirectoryName(game)!, "out");
        var lines = new List<string>();

        var result = new MapExporter(install).Export("bt20_02", outDir, progress: lines.Add);

        Assert.True(File.Exists(result.GlbPath));
        var man = JsonSerializer.Deserialize(
            File.ReadAllText(result.ManifestPath),
            ManifestJson.Default.MapManifest)!;
        Assert.Equal("bt20_02", man.StageId);
        Assert.Equal("battle", man.Category);
        Assert.True(man.Models.Single().Exported);
        Assert.Contains(lines, l => l.Contains("baking model 1/1"));
        Assert.Contains(lines, l => l.StartsWith("writing bt20_02.glb"));
    }
}
