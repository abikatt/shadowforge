using System.Text;
using ShadowForge.Formats.BDSL;
using ShadowForge.Scene.Script.Lift;

namespace ShadowForge.Tests.BDSL;

public sealed class StatementWriterTests
{
    private static string W(params Statement[] stmts)
    {
        var sb = new StringBuilder();
        StatementWriter.Write(sb, stmts, 0);
        return sb.ToString().Replace("\r\n", "\n");
    }

    [Fact]
    public void Call_OmitsZeroArgs_UsesNamesAndKinds()
    {
        Assert.Equal("show_message(msg=1070)\n", W(new CallStmt(5001, [1070, 0, 0, 0, 0, 0], false)));
        Assert.Equal("show_message(msg=1070, auto_advance=1, delay=10)\n",
            W(new CallStmt(5001, [1070, 0, 1, 10, 0, 0], false)));
        Assert.Equal("npc_action()\n", W(new CallStmt(5027, [0, 0, 0, 0, 0, 0], false)));
        Assert.Equal("effect(p2=1, hash=0xB6E2E7)\n", W(new CallStmt(5060, [0, 0, 1, 0xB6E2E7, 0, 0], false)));
        Assert.Equal("player_teleport(x=4.0)\n", W(new CallStmt(5029, [0x40800000, 0, 0, 0, 0, 0], false)));
        Assert.Equal("heal(chara=kluke, target=1)\n", W(new CallStmt(5008, [2, 1, 0, 0, 0, 0], false)));
        Assert.Equal("op_5002(p1=5)\n", W(new CallStmt(5002, [0, 5, 0, 0, 0, 0], false)));
        Assert.Equal("!wait(type=2)\n", W(new CallStmt(5020, [2, 0, 0, 0, 0, 0], true)));
    }

    [Fact]
    public void Aliases_UseDistinctNames()
    {
        Assert.Equal("show_message_b(msg=1)\n", W(new CallStmt(5067, [1, 0, 0, 0, 0, 0], false)));
        Assert.Equal("if_chest()\n", W(new CallStmt(5096, new uint[10], false)));
    }

    [Fact]
    public void Simple_Statements()
    {
        Assert.Equal("@8:\n", W(new LabelStmt(8, false)));
        Assert.Equal("!@8:\n", W(new LabelStmt(8, true)));
        Assert.Equal("goto @8\n", W(new GotoStmt(8, false)));
        Assert.Equal("end\n", W(new EndStmt(false)));
        Assert.Equal("!end\n", W(new EndStmt(true)));
        Assert.Equal("// \u5BBF\u5C4B\n", W(new CommentStmt("\u5BBF\u5C4B")));
        Assert.Equal("raw DEADBEEF\n", W(new RawStmt([0xDE, 0xAD, 0xBE, 0xEF])));
    }

    [Fact]
    public void Assign_Forms()
    {
        Assert.Equal("var[3362] = 1\n", W(new AssignStmt([3362, 0, 0, 1, 0, 0], false)));
        Assert.Equal("var[5] += 5\n", W(new AssignStmt([5, 1, 0, 5, 0, 0], false)));
        Assert.Equal("var[5] -= var[300]\n", W(new AssignStmt([5, 2, 1, 300, 0, 0], false)));
        Assert.Equal("var[5] *= random(1, 6)\n", W(new AssignStmt([5, 3, 2, (1u << 16) | 6, 0, 0], false)));
        Assert.Equal("var[5] /= chara(kluke, 2)\n", W(new AssignStmt([5, 4, 3, (2u << 16) | 2, 0, 0], false)));
        Assert.Equal("var[5] = playtime\n", W(new AssignStmt([5, 0, 4, 0, 0, 0], false)));
        Assert.Equal("var[5] = gold\n", W(new AssignStmt([5, 0, 8, 0, 0, 0], false)));
        Assert.Equal("var[5] = encounters\n", W(new AssignStmt([5, 0, 100, 0, 0, 0], false)));
        Assert.Equal("!var[5] = 1\n", W(new AssignStmt([5, 0, 0, 1, 0, 0], true)));
    }

    [Fact]
    public void Flag_Forms()
    {
        Assert.Equal("flag[7] = 2\n", W(new FlagStmt([7, 0, 2, 0, 0, 0], false)));
        Assert.Equal("var[300] = flag[7]\n", W(new FlagStmt([7, 1, 300, 0, 0, 0], false)));
    }

    [Fact]
    public void If_Flat_Forms()
    {
        Assert.Equal("if var[1190] == 0 else goto @47\n", W(new IfStmt([0, 0, 1190, 0, 0, 0, 0, 2, 47, 0], false, null)));
        Assert.Equal("if var[5] >= var[6] then end\n", W(new IfStmt([0, 1, 5, 6, 1, 1, 0, 0, 0, 0], false, null)));
        Assert.Equal("if item_count(219) >= 1 then goto @8 else goto @9\n", W(new IfStmt([1, 0, 219, 1, 1, 2, 8, 2, 9, 0], false, null)));
        Assert.Equal("if inventory_count(219) < 1\n", W(new IfStmt([1, 1, 219, 1, 0, 0, 0, 0, 0, 0], false, null)));
        Assert.Equal("if !in_party(kluke) then end\n", W(new IfStmt([2, 0, 2, 0, 0, 1, 0, 0, 0, 0], false, null)));
        Assert.Equal("if in_party(zola) then goto @3\n", W(new IfStmt([2, 0, 4, 0, 1, 2, 3, 0, 0, 0], false, null)));
        Assert.Equal("if gold > 100 then end\n", W(new IfStmt([3, 0, 0, 100, 3, 1, 0, 0, 0, 0], false, null)));
        Assert.Equal("if realtime <= 60 then end\n", W(new IfStmt([4, 1, 0, 60, 2, 1, 0, 0, 0, 0], false, null)));
        Assert.Equal("if medals != 3 then end\n", W(new IfStmt([5, 0, 0, 3, 5, 1, 0, 0, 0, 0], false, null)));
        Assert.Equal("if chapter < 1679 then end\n", W(new IfStmt([6, 0, 0, 1679, 4, 1, 0, 0, 0, 0], false, null)));
        Assert.Equal("!if var[5] == 1 else goto @8\n", W(new IfStmt([0, 0, 5, 1, 0, 0, 0, 2, 8, 0], true, null)));
    }

    [Fact]
    public void If_Structured_Indents()
    {
        var stmt = new IfStmt([0, 0, 3362, 0, 0, 0, 0, 2, 3, 0], false, [new AssignStmt([3362, 0, 0, 1, 0, 0], false), new EndStmt(false)]);
        Assert.Equal("if var[3362] == 0 {\n    var[3362] = 1\n    end\n}\n@3:\n", W(stmt, new LabelStmt(3, false)));
    }

    [Fact]
    public void Overflow_Forms()
    {
        Assert.Equal("if overflow(item 34, +1) then goto @3\n", W(new OverflowIfStmt([0, 34, 1, 3, 0, 0], false, null)));
        Assert.Equal("if overflow(gold, +500) then goto @3\n", W(new OverflowIfStmt([1, 0, 500, 3, 0, 0], false, null)));
        Assert.Equal("if overflow(medals, +var[7]) then goto @3\n", W(new OverflowIfStmt([2, 0, 7, 3, 4, 0], false, null)));
        Assert.Equal("if overflow(item var[6], +1) then goto @3\n", W(new OverflowIfStmt([0, 6, 1, 3, 2, 0], false, null)));
        Assert.Equal("if !overflow(item 34, +1) {\n    end\n}\n", W(new OverflowIfStmt([0, 34, 1, 3, 0, 0], false, [new EndStmt(false)])));
    }

    [Fact]
    public void Battle_Forms()
    {
        Assert.Equal("battle(entry=12) on_win end\n", W(new BattleStmt([12, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0], false)));
        Assert.Equal("battle(entry=12, flags=0x3, p9=2) on_win goto @4 on_lose goto @5\n",
            W(new BattleStmt([12, 0, 0, 0, 3, 2, 4, 2, 5, 2, 0, 0, 0, 0], false)));
        Assert.Equal("battle(entry=12)\n", W(new BattleStmt([12, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], false)));
    }

    [Fact]
    public void Integers_AboveTheHexLimit_PrintAsHex()
    {
        Assert.Equal("if var[3362] == 0xFFFFFFFF then end\n", W(new IfStmt([0, 0, 3362, 0xFFFFFFFF, 0, 1, 0, 0, 0, 0], false, null)));
        Assert.Equal("if item_count(0x1000000) >= 0x2000000 then end\n",
            W(new IfStmt([1, 0, 0x1000000, 0x2000000, 1, 1, 0, 0, 0, 0], false, null)));
        Assert.Equal("if gold > 0x1000000 then end\n", W(new IfStmt([3, 0, 0, 0x1000000, 3, 1, 0, 0, 0, 0], false, null)));
        Assert.Equal("if overflow(gold, +0x1000000) then goto @3\n", W(new OverflowIfStmt([1, 0, 0x1000000, 3, 0, 0], false, null)));
        Assert.Equal("if overflow(item 0x1000000, +1) then goto @3\n", W(new OverflowIfStmt([0, 0x1000000, 1, 3, 0, 0], false, null)));
        Assert.Equal("flag[7] = 0x1000000\n", W(new FlagStmt([7, 0, 0x1000000, 0, 0, 0], false)));
        Assert.Equal("var[5] = 0xFFFFFFFF\n", W(new AssignStmt([5, 0, 0, 0xFFFFFFFF, 0, 0], false)));
    }

    [Fact]
    public void Call_WithArityOffTheTable_PinsTheLastWord()
    {
        Assert.Equal("op_5099(p0=1, p13=0)\n",
            W(new CallStmt(5099, [1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], false)));
        Assert.Equal("op_5099(p0=1)\n", W(new CallStmt(5099, [1, 0, 0, 0, 0, 0], false)));
    }

    [Fact]
    public void Init_Indents()
    {
        var init = new InitStmt([new CallStmt(5061, [0, 0, 0, 0, 0, 0], false)]);
        Assert.Equal("init {\n    animation_trigger()\n}\n", W(init));
        var sb = new StringBuilder();
        StatementWriter.Write(sb, [init], 8);
        Assert.Equal("        init {\n            animation_trigger()\n        }\n", sb.ToString().Replace("\r\n", "\n"));
    }
}
