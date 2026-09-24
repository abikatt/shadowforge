using System.Text;
using ShadowForge.Formats.BDSL;
using ShadowForge.Scene.Script.Lift;

namespace ShadowForge.Tests.BDSL;

public sealed class StatementParserTests
{
    private static List<Statement> P(string text) =>
        StatementParser.ParseBlock(text.Replace("\r\n", "\n").Split('\n'), 1);

    private static string Lowered(string text) => Bytecode.Describe(ScriptLowering.Lower(P(text)));

    private static string Lowered(params Statement[] stmts) => Bytecode.Describe(ScriptLowering.Lower(stmts));

    private static string RoundTrip(params Statement[] stmts)
    {
        var sb = new StringBuilder();
        StatementWriter.Write(sb, stmts, 0);
        return Lowered(sb.ToString());
    }

    [Fact]
    public void Call_KeywordArgs_PaddedToArity()
    {
        Assert.Equal(Lowered(new CallStmt(5001, [1070, 0, 1, 10, 0, 0], false)),
            Lowered("show_message(msg=1070, auto_advance=1, delay=10)"));
        Assert.Equal(Lowered(new CallStmt(5027, [0, 0, 0, 0, 0, 0], false)), Lowered("npc_action()"));
        Assert.Equal(Lowered(new CallStmt(5001, [1070, 0, 0, 0, 0, 7], false)), Lowered("show_message(p5=7, msg=1070)"));
        Assert.Equal(Lowered(new CallStmt(5020, [2, 0, 0, 0, 0, 0], true)), Lowered("!wait(type=2)"));
        Assert.Equal(Lowered(new CallStmt(5029, [0x40800000, 0, 0, 0, 0, 0], false)), Lowered("player_teleport(x=4.0)"));
        Assert.Equal(Lowered(new CallStmt(5008, [2, 0, 0, 0, 0, 0], false)), Lowered("heal(chara=kluke)"));
        Assert.Equal(Lowered(new CallStmt(5002, [0, 5, 0, 0, 0, 0], false)), Lowered("op_5002(p1=5)"));
    }

    [Fact]
    public void Call_UnknownName_Or_Arg_Throws()
    {
        var ex = Assert.Throws<FormatException>(() => P("frobnicate(x=1)"));
        Assert.Contains("line 1", ex.Message);
        Assert.Throws<FormatException>(() => P("show_message(nope=1)"));
        Assert.Throws<FormatException>(() => P("show_message(p126=1)"));
    }

    [Fact]
    public void Simple_Statements()
    {
        Assert.Equal(Lowered(new LabelStmt(8, false), new GotoStmt(8, true), new EndStmt(false), new RawStmt([0xDE, 0xAD])),
            Lowered("@8:\n!goto @8\nend\nraw DEAD"));
    }

    [Fact]
    public void Comment_IsNotStrippedAsHashComment()
    {
        var stmts = P("// price #3 \u5BBF");
        Assert.Equal("price #3 \u5BBF", Assert.IsType<CommentStmt>(Assert.Single(stmts)).Text);
        Assert.Empty(P("# ignored"));
    }

    [Fact]
    public void Assign_And_Flag_Forms()
    {
        Assert.Equal(Lowered(new AssignStmt([3362, 0, 0, 1, 0, 0], false)), Lowered("var[3362] = 1"));
        Assert.Equal(Lowered(new AssignStmt([5, 1, 0, 5, 0, 0], false)), Lowered("var[5] += 5"));
        Assert.Equal(Lowered(new AssignStmt([5, 2, 1, 300, 0, 0], false)), Lowered("var[5] -= var[300]"));
        Assert.Equal(Lowered(new AssignStmt([5, 3, 2, (1u << 16) | 6, 0, 0], false)), Lowered("var[5] *= random(1, 6)"));
        Assert.Equal(Lowered(new AssignStmt([5, 4, 3, (2u << 16) | 2, 0, 0], false)), Lowered("var[5] /= chara(kluke, 2)"));
        Assert.Equal(Lowered(new AssignStmt([5, 0, 100, 0, 0, 0], false)), Lowered("var[5] = encounters"));
        Assert.Equal(Lowered(new AssignStmt([5, 0, 0, 0xFFFFFFFF, 0, 0], true)), Lowered("!var[5] = -1"));
        Assert.Equal(Lowered(new FlagStmt([7, 0, 2, 0, 0, 0], false)), Lowered("flag[7] = 2"));
        Assert.Equal(Lowered(new FlagStmt([7, 1, 300, 0, 0, 0], false)), Lowered("var[300] = flag[7]"));
    }

    [Fact]
    public void If_Flat_Forms()
    {
        Assert.Equal(Lowered(new IfStmt([0, 0, 1190, 0, 0, 0, 0, 2, 47, 0], false, null)), Lowered("if var[1190] == 0 else goto @47"));
        Assert.Equal(Lowered(new IfStmt([0, 1, 5, 6, 1, 1, 0, 0, 0, 0], false, null)), Lowered("if var[5] >= var[6] then end"));
        Assert.Equal(Lowered(new IfStmt([1, 0, 219, 1, 1, 2, 8, 2, 9, 0], false, null)), Lowered("if item_count(219) >= 1 then goto @8 else goto @9"));
        Assert.Equal(Lowered(new IfStmt([1, 1, 219, 1, 0, 0, 0, 0, 0, 0], false, null)), Lowered("if inventory_count(219) < 1"));
        Assert.Equal(Lowered(new IfStmt([2, 0, 2, 0, 0, 1, 0, 0, 0, 0], false, null)), Lowered("if !in_party(kluke) then end"));
        Assert.Equal(Lowered(new IfStmt([2, 0, 4, 0, 1, 2, 3, 0, 0, 0], false, null)), Lowered("if in_party(zola) then goto @3"));
        Assert.Equal(Lowered(new IfStmt([3, 0, 0, 100, 3, 1, 0, 0, 0, 0], false, null)), Lowered("if gold > 100 then end"));
        Assert.Equal(Lowered(new IfStmt([4, 1, 0, 60, 2, 1, 0, 0, 0, 0], false, null)), Lowered("if realtime <= 60 then end"));
        Assert.Equal(Lowered(new IfStmt([6, 0, 0, 1679, 4, 1, 0, 0, 0, 0], false, null)), Lowered("if chapter < 1679 then end"));
        Assert.Equal(Lowered(new IfStmt([0, 0, 5, 1, 0, 0, 0, 2, 8, 0], true, null)), Lowered("!if var[5] == 1 else goto @8"));
        Assert.Equal(Lowered(new OverflowIfStmt([0, 6, 1, 3, 2, 0], false, null)), Lowered("if overflow(item var[6], +1) then goto @3"));
        Assert.Equal(Lowered(new OverflowIfStmt([2, 0, 7, 3, 4, 0], false, null)), Lowered("if overflow(medals, +var[7]) then goto @3"));
    }

    [Fact]
    public void If_Structured_TakesFollowingLabel()
    {
        var text = "if var[3362] == 0 {\n    var[3362] = 1\n    end\n}\n@3:\nnpc_action()";
        Assert.Equal(Lowered(
                new IfStmt([0, 0, 3362, 0, 0, 0, 0, 2, 3, 0], false, [new AssignStmt([3362, 0, 0, 1, 0, 0], false), new EndStmt(false)]),
                new LabelStmt(3, false),
                new CallStmt(5027, [0, 0, 0, 0, 0, 0], false)),
            Lowered(text));
    }

    [Fact]
    public void If_Structured_SynthesisesLabelWhenMissing()
    {
        var text = "@4:\nif var[1] == 0 {\n    end\n}\nif !overflow(item 2, +1) {\n    end\n}\nnpc_action()";
        var stmts = P(text);
        Assert.Equal(5u, Assert.IsType<IfStmt>(stmts[1]).FalseLabel);
        Assert.Equal(5u, Assert.IsType<LabelStmt>(stmts[2]).Id);
        Assert.Equal(6u, Assert.IsType<OverflowIfStmt>(stmts[3]).Label);
        Assert.Equal(6u, Assert.IsType<LabelStmt>(stmts[4]).Id);
    }

    [Fact]
    public void Init_And_Nesting()
    {
        var text = "init {\n    animation_trigger()\n    if var[1] == 0 {\n        effect(hash=0xB6E2E7)\n    }\n    @2:\n}\nshow_message(msg=1)";
        var stmts = P(text);
        var init = Assert.IsType<InitStmt>(stmts[0]);
        Assert.Equal(3, init.Body.Count);
        Assert.NotNull(Assert.IsType<IfStmt>(init.Body[1]).Then);
        Assert.IsType<CallStmt>(stmts[1]);
    }

    [Fact]
    public void Battle_Forms()
    {
        Assert.Equal(Lowered(new BattleStmt([12, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0], false)), Lowered("battle(entry=12) on_win end"));
        Assert.Equal(Lowered(new BattleStmt([12, 0, 0, 0, 3, 2, 4, 2, 5, 2, 0, 0, 0, 0], false)),
            Lowered("battle(entry=12, flags=0x3, p9=2) on_win goto @4 on_lose goto @5"));
    }

    [Fact]
    public void Errors_ReportLineNumbers()
    {
        var ex = Assert.Throws<FormatException>(() => StatementParser.ParseBlock(["end", "bogus line here"], 40));
        Assert.Contains("line 41", ex.Message);
        Assert.Throws<FormatException>(() => P("init {\nend"));
        Assert.Throws<FormatException>(() => P("!init {\n}"));
        Assert.Throws<FormatException>(() => P("!if var[1] == 0 {\n}"));
    }

    [Fact]
    public void Writer_Output_Parses_Back()
    {
        Statement[] stmts =
        [
            new InitStmt([new CallStmt(5061, [0, 0, 1, 0, 0, 0], false), new CallStmt(5060, [0, 0, 1, 0xB6E2E7, 0, 0], false)]),
            new CommentStmt("\u5BBF\u5C4B"),
            new IfStmt([0, 0, 3362, 0, 0, 0, 0, 2, 3, 0], false, [new OverflowIfStmt([0, 34, 1, 9, 0, 0], false, [new EndStmt(false)]), new LabelStmt(9, false)]),
            new LabelStmt(3, false),
            new BattleStmt([12, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0], true),
            new CallStmt(5021, [1, 601, 0x10, 0x42C80000, 0, 0, 0, 3, 0, 0], false),
            new RawStmt([1, 2, 3, 4]),
        ];
        Assert.Equal(Lowered(stmts), RoundTrip(stmts));
    }

    [Fact]
    public void BadArgumentValue_ReportsLineNumber()
    {
        var callEx = Assert.Throws<FormatException>(() => P("end\nshow_message(msg=abc)"));
        Assert.Contains("line 2", callEx.Message);
        var condEx = Assert.Throws<FormatException>(() => P("end\nif gold > abc then end"));
        Assert.Contains("line 2", condEx.Message);
    }

    [Fact]
    public void OverflowingTokens_ThrowFormatException_NotOverflowException()
    {
        Assert.Throws<FormatException>(() => P("var[5] = 0xFFFFFFFFF"));
        Assert.Throws<FormatException>(() => P("var[5] = var[99999999999]"));
    }

    [Fact]
    public void MalformedFollowingLabel_ReportsLabelLine()
    {
        var text = "if var[1] == 0 {\n    end\n}\n\n@abc:";
        var ex = Assert.Throws<FormatException>(() => P(text));
        Assert.Contains("line 5", ex.Message);
    }

    [Fact]
    public void Comment_PreservesTrailingWhitespace()
    {
        Assert.Equal("abc ", Assert.IsType<CommentStmt>(Assert.Single(P("// abc "))).Text);
        Assert.Equal(" abc", Assert.IsType<CommentStmt>(Assert.Single(P("//  abc"))).Text);
    }

    [Fact]
    public void Comment_TrailingWhitespace_SurvivesWriteParseLowerRoundTrip()
    {
        Statement[] stmts = [new CommentStmt("hello ")];
        Assert.Equal(Lowered(stmts), RoundTrip(stmts));
    }

    [Fact]
    public void Call_MoreWordsThanTable_SurvivesWriteParseLower()
    {
        var instr = Bytecode.I(5099, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 0);
        var lifted = ScriptLifter.Lift([instr]);
        var sb = new StringBuilder();
        StatementWriter.Write(sb, lifted, 0);
        Assert.Contains("p13=0", sb.ToString());
        Assert.Equal(Bytecode.Describe([instr]), Lowered(sb.ToString()));
    }

    [Fact]
    public void Call_ArgumentsBelowTableArity_PadToTheTableArity()
    {
        Assert.Equal("00001389:32:1,0,0,0,0,0", Lowered("show_message(msg=1)"));
        Assert.Equal("00001389:32:1,0,0,0,0,0", Lowered("show_message(msg=1, p1=0)"));
    }

    [Fact]
    public void Block_FollowedByLineComment_UsesTheRealLabel()
    {
        var stmts = P("if var[5] == 1 {\n    end\n}\n// note\n@7:");
        Assert.Equal(3, stmts.Count);
        Assert.Equal(7u, Assert.IsType<IfStmt>(stmts[0]).FalseLabel);
        Assert.Equal("note", Assert.IsType<CommentStmt>(stmts[1]).Text);
        Assert.Equal(7u, Assert.IsType<LabelStmt>(stmts[2]).Id);
    }

    [Fact]
    public void HexIntegers_SurviveWriteParseLower()
    {
        Statement[] stmts =
        [
            new IfStmt([0, 0, 3362, 0xFFFFFFFF, 0, 1, 0, 0, 0, 0], false, null),
            new OverflowIfStmt([1, 0, 0x1000000, 3, 0, 0], false, null),
            new FlagStmt([7, 0, 0x1000000, 0, 0, 0], false),
            new AssignStmt([5, 0, 0, 0xFFFFFFFF, 0, 0], false),
        ];
        Assert.Equal(Lowered(stmts), RoundTrip(stmts));
    }

    [Fact]
    public void Call_RepeatedArgumentIndex_Throws()
    {
        var ex = Assert.Throws<FormatException>(() => P("set_variable(dest=1, p0=2)"));
        Assert.Contains("line 1", ex.Message);
        Assert.Contains("repeats index 0", ex.Message);
        Assert.Throws<FormatException>(() => P("show_message(msg=1, msg=2)"));
    }

    [Fact]
    public void UnknownBlockHead_ReportsHeadLine()
    {
        var text = "end\nfoo {\n    end\n}";
        var ex = Assert.Throws<FormatException>(() => P(text));
        Assert.Contains("line 2", ex.Message);
    }

    [Fact]
    public void Block_Head_Errors_ReportHeadLine()
    {
        var text = "end\nif bogus_condition {\n    end\n    end\n    end\n}";
        var ex = Assert.Throws<FormatException>(() => P(text));
        Assert.Contains("line 2", ex.Message);
    }
}
