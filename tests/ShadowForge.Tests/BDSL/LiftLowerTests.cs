using ShadowForge.Scene.Script;
using ShadowForge.Scene.Script.Lift;

namespace ShadowForge.Tests.BDSL;

public sealed class LiftLowerTests
{
    private static readonly uint D = OpcodeTable.DisabledFlag;

    private static void AssertIdentity(params ScriptElement[] elements)
    {
        var lifted = ScriptLifter.Lift(elements);
        var lowered = ScriptLowering.Lower(lifted);
        Assert.Equal(Bytecode.Describe(elements), Bytecode.Describe(lowered));
    }

    [Fact]
    public void PlainCall_KeepsArgsAndSize()
    {
        var stmts = ScriptLifter.Lift([Bytecode.I(5001, 0x3E8, 0, 0, 0, 0, 0)]);
        var call = Assert.IsType<CallStmt>(Assert.Single(stmts));
        Assert.Equal(5001u, call.Opcode);
        Assert.Equal(6, call.Args.Length);
        AssertIdentity(Bytecode.I(5001, 0x3E8, 0, 0, 0, 0, 0));
    }

    [Fact]
    public void Label_Goto_End()
    {
        var stmts = ScriptLifter.Lift([Bytecode.I(5000, 3, 0), Bytecode.I(5013, 3, 0), Bytecode.I(5023, 0, 0)]);
        Assert.Equal(3u, Assert.IsType<LabelStmt>(stmts[0]).Id);
        Assert.Equal(3u, Assert.IsType<GotoStmt>(stmts[1]).Label);
        Assert.IsType<EndStmt>(stmts[2]);
        AssertIdentity(Bytecode.I(5000, 3, 0), Bytecode.I(5013, 3, 0), Bytecode.I(5023, 0, 0));
    }

    [Fact]
    public void Label_WithNonZeroSecondWord_StaysCall()
    {
        var stmts = ScriptLifter.Lift([Bytecode.I(5000, 3, 7)]);
        Assert.IsType<CallStmt>(Assert.Single(stmts));
        AssertIdentity(Bytecode.I(5000, 3, 7));
    }

    [Fact]
    public void Disabled_Flag_Survives()
    {
        var stmts = ScriptLifter.Lift([Bytecode.I(5013 | D, 3, 0), Bytecode.I(5000 | D, 3, 0)]);
        Assert.True(Assert.IsType<GotoStmt>(stmts[0]).Disabled);
        Assert.True(Assert.IsType<LabelStmt>(stmts[1]).Disabled);
        AssertIdentity(Bytecode.I(5013 | D, 3, 0), Bytecode.I(5000 | D, 3, 0));
    }

    [Fact]
    public void Comment_IsDecoded()
    {
        var words = CommentText.Encode("hello");
        var stmts = ScriptLifter.Lift([Bytecode.I(5050, words)]);
        Assert.Equal("hello", Assert.IsType<CommentStmt>(Assert.Single(stmts)).Text);
        AssertIdentity(Bytecode.I(5050, words));
    }

    [Fact]
    public void Comment_Disabled_StaysCall()
    {
        var words = CommentText.Encode("hello");
        var stmts = ScriptLifter.Lift([Bytecode.I(5050 | D, words)]);
        Assert.IsType<CallStmt>(Assert.Single(stmts));
        AssertIdentity(Bytecode.I(5050 | D, words));
    }

    [Fact]
    public void Comment_WithLineBreak_StaysCall()
    {
        uint[] words = [0x000A0041, 0x42, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        Assert.IsType<CallStmt>(Assert.Single(ScriptLifter.Lift([Bytecode.I(5050, words)])));
        AssertIdentity(Bytecode.I(5050, words));
    }

    [Fact]
    public void Assign_And_Flag()
    {
        var a = Bytecode.I(5003, 0xD22, 1, 0, 5, 0, 0);
        var f = Bytecode.I(5063, 7, 0, 2, 0, 0, 0);
        var g = Bytecode.I(5063, 7, 1, 300, 0, 0, 0);
        var stmts = ScriptLifter.Lift([a, f, g]);
        var assign = Assert.IsType<AssignStmt>(stmts[0]);
        Assert.Equal(0xD22u, assign.Dest); Assert.Equal(1u, assign.Op); Assert.Equal(5u, assign.Value);
        Assert.False(Assert.IsType<FlagStmt>(stmts[1]).IsGet);
        Assert.True(Assert.IsType<FlagStmt>(stmts[2]).IsGet);
        AssertIdentity(a, f, g);
    }

    [Fact]
    public void Assign_WithUnusedWordsSet_StaysCall()
    {
        var a = Bytecode.I(5003, 0xD22, 1, 0, 5, 0, 9);
        Assert.IsType<CallStmt>(Assert.Single(ScriptLifter.Lift([a])));
        AssertIdentity(a);
    }

    [Fact]
    public void IfExtended_Flat()
    {
        var i = Bytecode.I(5012, 0, 0, 0x4A6, 0, 0, 0, 0, 2, 0x2F, 0);
        var s = Assert.IsType<IfStmt>(Assert.Single(ScriptLifter.Lift([i])));
        Assert.Null(s.Then);
        Assert.Equal(2u, s.FalseAction); Assert.Equal(0x2Fu, s.FalseLabel);
        AssertIdentity(i);
    }

    [Fact]
    public void IfExtended_UnknownCheckType_StaysCall()
    {
        var i = Bytecode.I(5012, 9, 0, 1, 0, 0, 0, 0, 2, 5, 0);
        Assert.IsType<CallStmt>(Assert.Single(ScriptLifter.Lift([i])));
        AssertIdentity(i);
    }

    [Fact]
    public void Overflow_And_Battle()
    {
        var o = Bytecode.I(5042, 0, 0x22, 1, 3, 0, 0);
        var b = Bytecode.I(5016, 12, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0);
        var stmts = ScriptLifter.Lift([o, b]);
        Assert.Equal(3u, Assert.IsType<OverflowIfStmt>(stmts[0]).Label);
        Assert.Equal(1u, Assert.IsType<BattleStmt>(stmts[1]).WinAction);
        AssertIdentity(o, b);
    }

    [Fact]
    public void InitSection_IsGrouped()
    {
        var elems = new ScriptElement[]
        {
            Bytecode.I(5057, 0, 0),
            Bytecode.I(5061, 0, 0, 1, 0, 0, 0),
            Bytecode.I(5058, 0, 0),
            Bytecode.I(5001, 0x3E8, 0, 0, 0, 0, 0),
        };
        var stmts = ScriptLifter.Lift(elems);
        Assert.Equal(2, stmts.Count);
        var init = Assert.IsType<InitStmt>(stmts[0]);
        Assert.Single(init.Body);
        AssertIdentity(elems);
    }

    [Fact]
    public void InitSection_Unmatched_StaysCalls()
    {
        var elems = new ScriptElement[] { Bytecode.I(5057, 0, 0), Bytecode.I(5001, 1, 0, 0, 0, 0, 0) };
        var stmts = ScriptLifter.Lift(elems);
        Assert.Equal("init_begin", OpcodeTable.Get(Assert.IsType<CallStmt>(stmts[0]).Opcode).Name);
        AssertIdentity(elems);
    }

    [Fact]
    public void RawData_Passes_Through()
    {
        var elems = new ScriptElement[] { new ScriptData { Bytes = [0xDE, 0xAD] }, Bytecode.I(5023, 0, 0) };
        Assert.IsType<RawStmt>(ScriptLifter.Lift(elems)[0]);
        AssertIdentity(elems);
    }

    [Fact]
    public void MoreWordsThanTable_StaysCall_AndKeepsSize()
    {
        var instr = Bytecode.I(5099, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 0);
        var call = Assert.IsType<CallStmt>(Assert.Single(ScriptLifter.Lift([instr])));
        Assert.Equal(14, call.Args.Length);
        AssertIdentity(instr);
    }

    [Fact]
    public void FewerWordsThanTable_FailsAtLift()
    {
        var ex = Assert.Throws<FormatException>(() => ScriptLifter.Lift([Bytecode.I(5001, 0x3E8, 0)]));
        Assert.Contains("5001", ex.Message);
        Assert.Contains("2 argument words", ex.Message);
    }

    [Fact]
    public void SizeThatIsNotEightPlusWords_FailsAtLift()
    {
        var instr = new ScriptInstruction { Opcode = 5023, Size = 10, RawParams = [0, 0] };
        var ex = Assert.Throws<FormatException>(() => ScriptLifter.Lift([instr]));
        Assert.Contains("5023", ex.Message);
        Assert.Contains("size 10", ex.Message);
    }

    [Fact]
    public void Lower_SetsSizeFromArgs()
    {
        var lowered = ScriptLowering.Lower([new CallStmt(5001, [1, 0, 0, 0, 0, 0], false), new EndStmt(false)]);
        Assert.Equal(32u, ((ScriptInstruction)lowered[0]).Size);
        Assert.Equal(16u, ((ScriptInstruction)lowered[1]).Size);
    }
}
