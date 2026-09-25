using ShadowForge.GameData.Maps;
using ShadowForge.GameData.Scripts;
using ShadowForge.Scene;

namespace ShadowForge.Tests.GameData;

public sealed class SceneScriptCatalogTests
{
    private static readonly HashSet<string> Stages = new(StringComparer.OrdinalIgnoreCase)
    {
        "bg01_01", "dg03_02", "bi03a01", "dg08_02",
    };

    [Theory]
    [InlineData(@"D:\DB\script\data\mes\mes_bg01_01.u16616\mes_bg01_01.u16", AreaType.Town, 101u, "bg01_01")]
    [InlineData(@"C:\iiduka_works\message\mes_dg03_02.u1603_02.u16", AreaType.Dungeon, 302u, "dg03_02")]
    [InlineData(@"D:\indoor_mes\mes_bi03a01.u16", AreaType.Indoor, 301u, "bi03a01")]
    public void StageFor_ReadsTheStageFromTheMessagePath_DespiteTrailingJunk(
        string messagePath, AreaType area, uint stageId, string expected) =>
        Assert.Equal(expected, SceneScriptCatalog.StageFor(messagePath, area, stageId, Stages));

    [Fact]
    public void StageFor_FallsBackToAreaAndStageId_WhenThereIsNoMessagePath() =>
        Assert.Equal("dg08_02", SceneScriptCatalog.StageFor("", AreaType.Dungeon, 802u, Stages));

    [Theory]
    [InlineData(@"D:\mes\mes_dg12_04.u16", AreaType.Dungeon, 1204u)]
    [InlineData("", AreaType.Indoor, 301u)]
    public void StageFor_IsNull_ForStagesTheInstallDoesNotList(string messagePath, AreaType area, uint stageId) =>
        Assert.Null(SceneScriptCatalog.StageFor(messagePath, area, stageId, Stages));

    [SkippableFact]
    public void List_RetailScripts_TiesTaltaVillageToItsScript()
    {
        var files = RetailData.Files;
        var stages = new MapCatalog(RetailData.Install).List().Stages
            .Select(s => s.StageId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var scripts = new SceneScriptCatalog(files).List(stages);
        Skip.If(scripts.Count == 0, "no scene scripts in the located game data");

        var talta = Assert.Single(scripts, s => s.FileName == "sc01_0001_01");
        Assert.Equal(@"script\town\sc01_0001_01.rpj", talta.VfsPath);
        Assert.Equal("town", talta.Area);
        Assert.Equal("bg01_01", talta.StageId);
    }
}
