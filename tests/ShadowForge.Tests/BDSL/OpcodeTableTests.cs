using ShadowForge.Scene.Script;

namespace ShadowForge.Tests.BDSL;

public sealed class OpcodeTableTests
{
    [Fact]
    public void EveryOpcodeHasFixedWordCount()
    {
        for (uint op = 5000; op <= 5100; op++)
        {
            var spec = OpcodeTable.Get(op);
            Assert.Contains(spec.Words, new[] { 2, 6, 10, 14 });
            Assert.Equal(op, spec.Opcode);
        }
    }

    [Theory]
    [InlineData(5001u, "show_message", 6)]
    [InlineData(5012u, "if_extended", 10)]
    [InlineData(5016u, "battle", 14)]
    [InlineData(5050u, "comment", 14)]
    [InlineData(5057u, "init_begin", 2)]
    [InlineData(5058u, "init_end", 2)]
    [InlineData(5067u, "show_message_b", 6)]
    [InlineData(5068u, "transition2_b", 10)]
    [InlineData(5070u, "fade_transition_b", 6)]
    [InlineData(5096u, "if_chest", 10)]
    [InlineData(5097u, "set_variable_chest", 6)]
    [InlineData(5002u, "op_5002", 6)]
    public void NamesAndWords(uint opcode, string name, int words)
    {
        var spec = OpcodeTable.Get(opcode);
        Assert.Equal(name, spec.Name);
        Assert.Equal(words, spec.Words);
    }

    [Fact]
    public void DisabledBitIsIgnoredByGet()
    {
        Assert.Equal("show_message", OpcodeTable.Get(5001 | OpcodeTable.DisabledFlag).Name);
    }

    [Fact]
    public void NamesAreUnique()
    {
        var names = new HashSet<string>();
        for (uint op = 5000; op <= 5100; op++)
            Assert.True(names.Add(OpcodeTable.Get(op).Name), $"duplicate name for {op}");
    }

    [Fact]
    public void TryGetByName_RoundTrips()
    {
        for (uint op = 5000; op <= 5100; op++)
        {
            Assert.True(OpcodeTable.TryGetByName(OpcodeTable.Get(op).Name, out var spec));
            Assert.Equal(op, spec.Opcode);
        }
        Assert.False(OpcodeTable.TryGetByName("no_such_opcode", out _));
    }

    [Fact]
    public void ArgAt_FallsBackToPositionalName()
    {
        var spec = OpcodeTable.Get(5001);
        Assert.Equal("msg", spec.ArgAt(0).Name);
        Assert.Equal("p5", spec.ArgAt(5).Name);
        Assert.Equal(ArgKind.Int, spec.ArgAt(5).Kind);
        Assert.Equal(0, spec.IndexOfArg("msg"));
        Assert.Equal(5, spec.IndexOfArg("p5"));
        Assert.Equal(13, spec.IndexOfArg("p13"));
        Assert.Equal(-1, spec.IndexOfArg("nope"));
        Assert.Equal(-1, spec.IndexOfArg("p126"));
    }

    [Fact]
    public void KindsForKnownArguments()
    {
        Assert.Equal(ArgKind.Float, OpcodeTable.Get(5021).ArgAt(3).Kind);
        Assert.Equal(ArgKind.Label, OpcodeTable.Get(5042).ArgAt(3).Kind);
        Assert.Equal(ArgKind.Var, OpcodeTable.Get(5028).ArgAt(9).Kind);
        Assert.Equal(ArgKind.Hex, OpcodeTable.Get(5060).ArgAt(3).Kind);
        Assert.Equal(ArgKind.Enum, OpcodeTable.Get(5008).ArgAt(0).Kind);
        Assert.Equal("kluke", OpcodeTable.Get(5008).ArgAt(0).EnumNames![2]);
    }

    [Fact]
    public void Get_RejectsOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OpcodeTable.Get(4999));
        Assert.Throws<ArgumentOutOfRangeException>(() => OpcodeTable.Get(5101));
    }
}
