using ShadowForge.GameData;

namespace ShadowForge.Tests.GameData;

public sealed class NameTablesTests
{
    private const string Characters =
        "DeleteKey,ID,翻訳対象,文字数\r\n" +
        ",pc01,Shu,全角４文字\r\n" +
        ",bs01,Nene,全角１２文字\r\n" +
        ",bs27,Guru-Guru,全角１２文字\r\n" +
        "1,ch100_10,Deleted Row,全角２０文字\r\n" +
        ",ch100_11,-,全角２０文字\r\n" +
        ",ch101_00,Fushira,全角２０文字\r\n" +
        ",em002,\"Poo Snake, Grassy\",全角１２文字\r\n";

    private const string Stages =
        "削除,MapID,\"warpID\r\nBMPカラーＩＤ\",正規表示名（１６文字以内）,カメラ,正規表示名（英語）,グローバル変数\r\n" +
        ",bg01_01,0,Talta Village,, ,-1\r\n" +
        ",bg01_01,4,Somewhere Else,, ,-1\r\n" +
        ",bg02_01,0,,,Sheep Tribe Camp - Wilderness,-1\r\n" +
        ",bg13_02,0,Jibral Castle Town,, ,-1\r\n";

    private static readonly NameTables Names = NameTables.FromText(Characters, Stages);

    [Theory]
    [InlineData("pc01", "Shu")]
    [InlineData("BS01", "Nene")]
    [InlineData("bs27_a", "Guru-Guru")]
    [InlineData("ch101", "Fushira")]
    [InlineData("em002", "Poo Snake, Grassy")]
    public void Character_FindsNameByIdVariantOrFirstCostume(string id, string expected) =>
        Assert.Equal(expected, Names.Character(id));

    [Theory]
    [InlineData("ch100_10")]
    [InlineData("ch100_11")]
    [InlineData("np999")]
    public void Character_SkipsDeletedAndDashRows_AndUnknownIds(string id) =>
        Assert.Null(Names.Character(id));

    [Theory]
    [InlineData("bg01_01", "Talta Village")]
    [InlineData("bg02_01", "Sheep Tribe Camp - Wilderness")]
    [InlineData("bg13_01", "Jibral Castle Town")]
    public void Stage_TakesFirstRowSecondNameColumnOrSameAreaSibling(string id, string expected) =>
        Assert.Equal(expected, Names.Stage(id));

    [Fact]
    public void Stage_UnknownAreaHasNoName() => Assert.Null(Names.Stage("bt01_01"));

    [Fact]
    public void ReadRecords_KeepsQuotedCommasAndLineBreaksInOneField()
    {
        var rows = NameTables.ReadRecords("a,\"b,\r\nc\",\"say \"\"hi\"\"\"\r\nd,e").ToList();

        Assert.Equal(2, rows.Count);
        Assert.Equal(["a", "b,\r\nc", "say \"hi\""], rows[0]);
        Assert.Equal(["d", "e"], rows[1]);
    }

    [SkippableFact]
    public void Load_RetailTables_NameKnownCharactersAndStages()
    {
        var names = NameTables.Load(RetailData.Files);
        Skip.If(names.Character("pc01") is null, "namelist tables are not in the located game data");

        Assert.Equal("Shu", names.Character("pc01"));
        Assert.Equal("Kluke", names.Character("pc03"));
        Assert.Equal("Talta Village", names.Stage("bg01_01"));
    }
}
