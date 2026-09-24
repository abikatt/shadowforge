using ShadowForge.Formats.XACT;

namespace ShadowForge.Tests.Formats.XACT;

public sealed class SeListTests
{
    [Fact]
    public void Parse_NumbersRowsFromZeroAfterTheHeader()
    {
        var list = SeList.Parse(Table());

        Assert.Equal(4, list.Entries.Count);

        var first = list.Find(0)!;
        Assert.Equal("ae01", first.Bank);
        Assert.Equal("se_mapg162", first.Cue);
        Assert.Equal(SeResidency.InMemory, first.Residency);
        Assert.False(first.IsStreaming);
        Assert.Equal("", first.Reverb);

        var streaming = list.Find(1)!;
        Assert.Equal("se_even021", streaming.Bank);
        Assert.True(streaming.IsStreaming);
        Assert.Equal("DEFAULT", streaming.Reverb);

        var system = list.Find(3)!;
        Assert.Equal("sys", system.Bank);
        Assert.Equal("se_menu023", system.Cue);
    }

    /// <summary>
    /// A blank row keeps its place so every id after it still matches the line
    /// it came from.
    /// </summary>
    [Fact]
    public void Parse_KeepsBlankRowsSoIdsStayAlignedWithLines()
    {
        var list = SeList.Parse(Table());
        Assert.True(list.Entries[2].IsBlank);
        Assert.Null(list.Find(2));
        Assert.Equal(3, list.Find(3)!.Id);
    }

    [Fact]
    public void BankRelativePath_FollowsResidency()
    {
        var list = SeList.Parse(Table());

        Assert.Equal(Path.Combine("snd_memory", "se", "ae01.xsb"), list.Find(0)!.SoundBankRelativePath);
        Assert.Equal(Path.Combine("snd_stream", "se", "se_even021.xwb"), list.Find(1)!.WaveBankRelativePath);
        Assert.Equal(
            Path.Combine(SeList.SystemSoundDirectory, "sys.xsb"),
            list.Find(3)!.SoundBankRelativePath);
    }

    [Fact]
    public void FindByCue_ReturnsEveryRowNamingIt()
    {
        var list = SeList.Parse(Table());
        var matches = list.FindByCue("se_menu023");
        Assert.Single(matches);
        Assert.Equal(3, matches[0].Id);
        Assert.Empty(list.FindByCue("se_absent"));
    }

    /// <summary>
    /// The shipped table pins the id rule: se_preb094 is on line 949 and is SE
    /// id 947, se_menu023 is on line 1875 and is SE id 1873.
    /// </summary>
    [SkippableFact]
    public void Parse_Shipped_IdIsTheLineNumberLessTwo()
    {
        var list = SeList.Parse(RetailData.Read(@"!necessity\bd_system\sound\selist.csv"));

        var enemy = list.Find(947)!;
        Assert.Equal("se_preb094", enemy.Cue);
        Assert.Equal("fdenm", enemy.Bank);
        Assert.False(enemy.IsStreaming);

        var menu = list.Find(1873)!;
        Assert.Equal("se_menu023", menu.Cue);
        Assert.Equal(SeList.SystemBank, menu.Bank);
    }

    private static byte[] Table()
    {
        string[] lines =
        [
            "#BANK,CUE,WAVEBANK,REVERB",
            "ae01,se_mapg162,INMEMORY,",
            "se_even021,se_even021,STREAMING,DEFAULT",
            "",
            "sys,se_menu023,INMEMORY,",
            "",
        ];
        return EncodingExtensions.ShiftJIS.GetBytes(string.Join("\r\n", lines));
    }
}
