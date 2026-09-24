using ShadowForge.Formats.BDSL;
using ShadowForge.Scene.Script;

namespace ShadowForge.Tests.BDSL;

public sealed class BlockConditionTextTests
{
    private static ScriptBlock Unconditional() => new() { ChapterMin = 0xFFFFFFFF, ChapterMax = 0 };

    [Fact]
    public void Format_Unconditional_IsEmpty() => Assert.Equal("", BlockConditionText.Format(Unconditional()));

    [Fact]
    public void Format_Parse_UnconditionalWithMaxSentinel_RoundTrips()
    {
        var b = new ScriptBlock { ChapterMin = 0xFFFFFFFF, ChapterMax = 0xFFFFFFFF };
        string header = BlockConditionText.Format(b);
        Assert.Equal("all..4294967295", header);

        var back = new ScriptBlock();
        BlockConditionText.Parse(back, header);
        Assert.Equal(0xFFFFFFFFu, back.ChapterMin);
        Assert.Equal(0xFFFFFFFFu, back.ChapterMax);
    }

    [Fact]
    public void Format_Chapter()
    {
        Assert.Equal("chapter 1679..9999", BlockConditionText.Format(new ScriptBlock { ChapterMin = 1679, ChapterMax = 9999 }));
        Assert.Equal("chapter 5..5", BlockConditionText.Format(new ScriptBlock { ChapterMin = 5, ChapterMax = 5 }));
        Assert.Equal("chapter 0..0", BlockConditionText.Format(new ScriptBlock()));
        Assert.Equal("all..7", BlockConditionText.Format(new ScriptBlock { ChapterMin = 0xFFFFFFFF, ChapterMax = 7 }));
    }

    [Fact]
    public void Format_Slots()
    {
        var b = Unconditional();
        b.Conditions[0] = new Condition(2, 0xD22, 0, 0);
        b.Conditions[1] = new Condition(3, 0x25, 1, 1);
        b.Conditions[2] = new Condition(4, 0b00101, 1, 0);
        b.Conditions[3] = new Condition(4, 0b10000, 0, 0);
        b.Conditions[4] = new Condition(5, 2, 0, 7);
        b.Conditions[5] = new Condition(5, 2, 1, 0);
        b.Conditions[6] = new Condition(9, 1, 2, 3);
        Assert.Equal("var[3362] == 0 and item_count(37) >= 1 and party_all(shu, kluke) and party_missing(zola) and savebit(7) and global_4095 and cond(9, 1, 2, 3)",
            BlockConditionText.Format(b));
    }

    [Fact]
    public void Format_ChapterAndSlot()
    {
        var b = new ScriptBlock { ChapterMin = 1679, ChapterMax = 9999 };
        b.Conditions[0] = new Condition(2, 0, 0, 0);
        Assert.Equal("chapter 1679..9999 and var[0] == 0", BlockConditionText.Format(b));
    }

    [Theory]
    [InlineData("")]
    [InlineData("chapter 1679..9999")]
    [InlineData("chapter 5..5")]
    [InlineData("chapter 0..0")]
    [InlineData("all..7")]
    [InlineData("var[3362] == 0 and item_count(37) >= 1 and party_all(shu, kluke) and party_missing(zola) and savebit(7) and global_4095 and cond(9, 1, 2, 3)")]
    [InlineData("chapter 1679..9999 and var[0] != 3 and item_count(2) < 1")]
    public void Parse_Format_RoundTrips(string header)
    {
        var b = new ScriptBlock();
        BlockConditionText.Parse(b, header);
        Assert.Equal(header, BlockConditionText.Format(b));
    }

    [Fact]
    public void Parse_AcceptsAllAndCommas()
    {
        var b = new ScriptBlock();
        BlockConditionText.Parse(b, "all");
        Assert.True(b.IsUnconditional);
        BlockConditionText.Parse(b, "chapter 0..59, var[6] == 0");
        Assert.Equal(59u, b.ChapterMax);
        Assert.Equal(6u, b.Conditions[0].Operand);
        BlockConditionText.Parse(b, "chapter 5");
        Assert.Equal(5u, b.ChapterMin);
        Assert.Equal(5u, b.ChapterMax);
        BlockConditionText.Parse(b, "party_all(shu, kluke), var[1] == 2");
        Assert.Equal(0b101u, b.Conditions[0].Operand);
        Assert.Equal(1u, b.Conditions[1].Operand);
    }

    [Fact]
    public void Parse_RejectsUnknown() =>
        Assert.Throws<FormatException>(() => BlockConditionText.Parse(new ScriptBlock(), "banana == 1"));
}
