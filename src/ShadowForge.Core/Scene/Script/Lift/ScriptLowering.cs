namespace ShadowForge.Scene.Script.Lift;

/// <summary>
/// Turns a statement tree back into bytecode elements.
/// </summary>
public static class ScriptLowering
{
    /// <summary>
    /// Each instruction size is 8 plus 4 per argument word.
    /// </summary>
    public static List<ScriptElement> Lower(IReadOnlyList<Statement> statements)
    {
        var output = new List<ScriptElement>();
        Emit(statements, output);
        return output;
    }

    private static void Emit(IReadOnlyList<Statement> statements, List<ScriptElement> output)
    {
        foreach (var stmt in statements)
        {
            switch (stmt)
            {
                case RawStmt raw:
                    output.Add(new ScriptData { Bytes = raw.Bytes });
                    break;
                case CallStmt call:
                    output.Add(Instr(call.Opcode, call.Disabled, call.Args));
                    break;
                case LabelStmt label:
                    output.Add(Instr(OpcodeTable.Label, label.Disabled, [label.Id, 0]));
                    break;
                case GotoStmt go:
                    output.Add(Instr(OpcodeTable.GotoLabel, go.Disabled, [go.Label, 0]));
                    break;
                case EndStmt end:
                    output.Add(Instr(OpcodeTable.EndScript, end.Disabled, [0, 0]));
                    break;
                case CommentStmt comment:
                    output.Add(Instr(OpcodeTable.Comment, false, CommentText.Encode(comment.Text)));
                    break;
                case InitStmt init:
                    output.Add(Instr(OpcodeTable.InitBegin, false, [0, 0]));
                    Emit(init.Body, output);
                    output.Add(Instr(OpcodeTable.InitEnd, false, [0, 0]));
                    break;
                case AssignStmt assign:
                    output.Add(Instr(OpcodeTable.SetVariable, assign.Disabled, assign.Args));
                    break;
                case FlagStmt flag:
                    output.Add(Instr(OpcodeTable.FlagGetSet, flag.Disabled, flag.Args));
                    break;
                case IfStmt cond:
                    output.Add(Instr(OpcodeTable.IfExtended, cond.Disabled, cond.Args));
                    if (cond.Then is not null) Emit(cond.Then, output);
                    break;
                case OverflowIfStmt overflow:
                    output.Add(Instr(OpcodeTable.Overflow, overflow.Disabled, overflow.Args));
                    if (overflow.Then is not null) Emit(overflow.Then, output);
                    break;
                case BattleStmt battle:
                    output.Add(Instr(OpcodeTable.Battle, battle.Disabled, battle.Args));
                    break;
                default:
                    throw new InvalidOperationException($"unknown statement {stmt.GetType().Name}");
            }
        }
    }

    private static ScriptInstruction Instr(uint opcode, bool disabled, uint[] args) => new()
    {
        Opcode = disabled ? opcode | OpcodeTable.DisabledFlag : opcode,
        Size = (uint)(8 + 4 * args.Length),
        RawParams = args,
    };
}
