using ShadowForge.Scene.Script;
using ShadowForge.Scene.Script.Lift;
using static ShadowForge.Tests.BDSL.Bytecode;

namespace ShadowForge.Tests.BDSL;

public sealed class StructuringTests
{
    private static readonly uint D = OpcodeTable.DisabledFlag;

    private static ScriptInstruction IfVarEqGoto(uint var, uint value, uint label) =>
        I(5012, 0, 0, var, value, 0, 0, 0, 2, label, 0);

    private static void AssertIdentity(IReadOnlyList<ScriptElement> elements) =>
        Assert.Equal(Describe(elements), Describe(ScriptLowering.Lower(ScriptLifter.Lift(elements))));

    [Fact]
    public void ForwardJump_BecomesBlock_LabelKept()
    {
        var elems = new ScriptElement[]
        {
            IfVarEqGoto(5, 1, 8),
            I(5001, 10, 0, 0, 0, 0, 0),
            I(5023, 0, 0),
            I(5000, 8, 0),
            I(5001, 11, 0, 0, 0, 0, 0),
        };
        var stmts = ScriptLifter.Lift(elems);
        Assert.Equal(3, stmts.Count);
        var cond = Assert.IsType<IfStmt>(stmts[0]);
        Assert.NotNull(cond.Then);
        Assert.Equal(2, cond.Then!.Count);
        Assert.Equal(8u, Assert.IsType<LabelStmt>(stmts[1]).Id);
        AssertIdentity(elems);
    }

    [Fact]
    public void Overflow_BecomesBlock()
    {
        var elems = new ScriptElement[]
        {
            I(5042, 0, 0x22, 1, 3, 0, 0),
            I(5004, 0x22, 1, 1, 1, 0, 0),
            I(5023, 0, 0),
            I(5000, 3, 0),
            I(5031, 0x14, 0x22, 1, 0, 0, 0),
        };
        var stmts = ScriptLifter.Lift(elems);
        var cond = Assert.IsType<OverflowIfStmt>(stmts[0]);
        Assert.Equal(2, cond.Then!.Count);
        AssertIdentity(elems);
    }

    [Fact]
    public void Nested_Blocks()
    {
        var elems = new ScriptElement[]
        {
            IfVarEqGoto(1, 0, 20),
            IfVarEqGoto(2, 0, 21),
            I(5001, 1, 0, 0, 0, 0, 0),
            I(5000, 21, 0),
            I(5001, 2, 0, 0, 0, 0, 0),
            I(5000, 20, 0),
        };
        var stmts = ScriptLifter.Lift(elems);
        var outer = Assert.IsType<IfStmt>(stmts[0]);
        var inner = Assert.IsType<IfStmt>(outer.Then![0]);
        Assert.Single(inner.Then!);
        Assert.Equal(21u, Assert.IsType<LabelStmt>(outer.Then![1]).Id);
        Assert.Equal(20u, Assert.IsType<LabelStmt>(stmts[1]).Id);
        AssertIdentity(elems);
    }

    [Fact]
    public void BackwardJump_StaysFlat()
    {
        var elems = new ScriptElement[]
        {
            I(5000, 8, 0),
            I(5001, 1, 0, 0, 0, 0, 0),
            IfVarEqGoto(5, 1, 8),
        };
        var stmts = ScriptLifter.Lift(elems);
        Assert.Equal(3, stmts.Count);
        Assert.Null(Assert.IsType<IfStmt>(stmts[2]).Then);
        AssertIdentity(elems);
    }

    [Fact]
    public void LabelInsideRegionTargetedFromOutside_StaysFlat()
    {
        var elems = new ScriptElement[]
        {
            I(5013, 9, 0),
            IfVarEqGoto(5, 1, 8),
            I(5000, 9, 0),
            I(5001, 1, 0, 0, 0, 0, 0),
            I(5000, 8, 0),
        };
        var stmts = ScriptLifter.Lift(elems);
        Assert.Equal(5, stmts.Count);
        Assert.Null(Assert.IsType<IfStmt>(stmts[1]).Then);
        AssertIdentity(elems);
    }

    [Fact]
    public void LabelInsideRegionTargetedFromInside_IsFine()
    {
        var elems = new ScriptElement[]
        {
            IfVarEqGoto(5, 1, 8),
            I(5013, 9, 0),
            I(5000, 9, 0),
            I(5000, 8, 0),
        };
        var stmts = ScriptLifter.Lift(elems);
        Assert.Equal(2, stmts.Count);
        Assert.Equal(2, Assert.IsType<IfStmt>(stmts[0]).Then!.Count);
        AssertIdentity(elems);
    }

    [Fact]
    public void DisabledIf_StaysFlat()
    {
        var elems = new ScriptElement[]
        {
            I(5012 | D, 0, 0, 5, 1, 0, 0, 0, 2, 8, 0),
            I(5001, 1, 0, 0, 0, 0, 0),
            I(5000, 8, 0),
        };
        Assert.Equal(3, ScriptLifter.Lift(elems).Count);
        AssertIdentity(elems);
    }

    [Fact]
    public void DisabledTargetLabel_StaysFlat()
    {
        var elems = new ScriptElement[]
        {
            IfVarEqGoto(5, 1, 8),
            I(5001, 1, 0, 0, 0, 0, 0),
            I(5000 | D, 8, 0),
        };
        Assert.Equal(3, ScriptLifter.Lift(elems).Count);
        AssertIdentity(elems);
    }

    [Fact]
    public void TrueGotoForm_StaysFlat()
    {
        var elems = new ScriptElement[]
        {
            I(5012, 0, 0, 5, 1, 0, 2, 8, 0, 0, 0),
            I(5001, 1, 0, 0, 0, 0, 0),
            I(5000, 8, 0),
        };
        Assert.Equal(3, ScriptLifter.Lift(elems).Count);
        AssertIdentity(elems);
    }

    [Fact]
    public void EmptyRegion_BecomesEmptyBlock()
    {
        var elems = new ScriptElement[] { IfVarEqGoto(5, 1, 8), I(5000, 8, 0) };
        var stmts = ScriptLifter.Lift(elems);
        Assert.Empty(Assert.IsType<IfStmt>(stmts[0]).Then!);
        AssertIdentity(elems);
    }

    [Fact]
    public void InitBody_IsStructured()
    {
        var elems = new ScriptElement[]
        {
            I(5057, 0, 0),
            IfVarEqGoto(5, 1, 8),
            I(5061, 0, 0, 0, 0, 0, 0),
            I(5000, 8, 0),
            I(5058, 0, 0),
        };
        var init = Assert.IsType<InitStmt>(ScriptLifter.Lift(elems)[0]);
        Assert.NotNull(Assert.IsType<IfStmt>(init.Body[0]).Then);
        AssertIdentity(elems);
    }
}
