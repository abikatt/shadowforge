using System.Text;
using ShadowForge.GameData.Mods;

namespace ShadowForge.Tests.GameData.Mods;

public sealed class AccessLogTests
{
    private const string Csv =
        "pack,inner_path,total_accesses,total_overrides,total_misses\r\n" +
        ",,3,0,3\r\n" +
        "database,battle\\motscr\\motscr_pc11.csv,1,1,0\r\n" +
        "!necessity,font\\font_02_01.kng,15,0,15\r\n";

    private static string WriteCsv()
    {
        string path = GameDataFixture.TempPath("sf_log_") + ".csv";
        File.WriteAllText(path, Csv, Encoding.ASCII);
        return path;
    }

    [Fact]
    public void Load_SkipsTheHeaderAndBlankKeyRows()
    {
        var rows = AccessLog.Load(WriteCsv());

        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, r => r.Pack.Length == 0);
    }

    [Fact]
    public void Load_ParsesTheCounts()
    {
        var row = AccessLog.Load(WriteCsv())
            .Single(r => r.InnerPath.EndsWith("motscr_pc11.csv", StringComparison.Ordinal));

        Assert.Equal("database", row.Pack);
        Assert.Equal(1, row.Accesses);
        Assert.Equal(1, row.Overrides);
        Assert.Equal(0, row.Misses);
    }

    [Fact]
    public void Find_MatchesAVfsPathAgainstPackPlusInnerPath()
    {
        var rows = AccessLog.Load(WriteCsv());

        var hit = AccessLog.Find(rows, @"database\battle\motscr\motscr_pc11.csv");

        Assert.NotNull(hit);
        Assert.Equal(1, hit!.Overrides);
    }

    [Fact]
    public void Find_ReturnsNullForAPathTheGameNeverRequested()
    {
        var rows = AccessLog.Load(WriteCsv());

        Assert.Null(AccessLog.Find(rows, @"chara\npc\np109\np109_01.dds"));
    }

    [Fact]
    public void Find_IsForwardSlashTolerant()
    {
        var rows = AccessLog.Load(WriteCsv());

        Assert.NotNull(AccessLog.Find(rows, "database/battle/motscr/motscr_pc11.csv"));
    }
}
