namespace ShadowForge.Scene.Script.Lift;

/// <summary>
/// Turns bytecode elements into a statement tree.
/// </summary>
public static class ScriptLifter
{
    /// <summary>
    /// Lowering the result reproduces the elements exactly.
    /// Throws FormatException for an instruction whose size or word count BDSL cannot express.
    /// </summary>
    public static List<Statement> Lift(IReadOnlyList<ScriptElement> elements)
    {
        var flat = new List<Statement>(elements.Count);
        foreach (var element in elements)
        {
            if (element is not ScriptInstruction instr)
            {
                flat.Add(new RawStmt(((ScriptData)element).Bytes));
                continue;
            }
            ThrowIfUnrepresentable(instr);
            flat.Add(FromInstruction(instr));
        }
        return Structure(GroupInit(flat));
    }

    private static List<Statement> Structure(List<Statement> statements)
    {
        var list = new List<Statement>(statements);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] is InitStmt init)
            {
                list[i] = init with { Body = Structure(new List<Statement>(init.Body)) };
                continue;
            }
            uint target;
            switch (list[i])
            {
                case IfStmt { Disabled: false, TrueAction: 0, FalseAction: 2, Then: null } cond:
                    target = cond.FalseLabel;
                    break;
                case OverflowIfStmt { Disabled: false, Then: null } overflow:
                    target = overflow.Label;
                    break;
                default:
                    continue;
            }
            int j = FindLabel(list, i + 1, target);
            if (j < 0) continue;
            var region = list.GetRange(i + 1, j - i - 1);
            if (!RegionIsClosed(list, i, j, region)) continue;
            var body = Structure(region);
            list[i] = list[i] switch
            {
                IfStmt c => c with { Then = body },
                OverflowIfStmt o => o with { Then = body },
                _ => throw new InvalidOperationException(),
            };
            list.RemoveRange(i + 1, region.Count);
        }
        return list;
    }

    private static int FindLabel(List<Statement> list, int from, uint id)
    {
        for (int j = from; j < list.Count; j++)
            if (list[j] is LabelStmt { Disabled: false } label && label.Id == id) return j;
        return -1;
    }

    private static bool RegionIsClosed(List<Statement> list, int i, int j, List<Statement> region)
    {
        var defined = new HashSet<uint>();
        foreach (var stmt in region) CollectLabels(stmt, defined);
        if (defined.Count == 0) return true;
        var outside = new HashSet<uint>();
        for (int k = 0; k < list.Count; k++)
        {
            if (k > i && k < j) continue;
            CollectReferences(list[k], outside);
        }
        return !defined.Overlaps(outside);
    }

    private static void CollectLabels(Statement stmt, HashSet<uint> into)
    {
        switch (stmt)
        {
            case LabelStmt label: into.Add(label.Id); break;
            case InitStmt init: foreach (var s in init.Body) CollectLabels(s, into); break;
            case IfStmt { Then: not null } c: foreach (var s in c.Then) CollectLabels(s, into); break;
            case OverflowIfStmt { Then: not null } o: foreach (var s in o.Then) CollectLabels(s, into); break;
        }
    }

    private static void CollectReferences(Statement stmt, HashSet<uint> into)
    {
        switch (stmt)
        {
            case GotoStmt go: into.Add(go.Label); break;
            case IfStmt c:
                if (c.TrueAction == 2) into.Add(c.TrueLabel);
                if (c.FalseAction == 2) into.Add(c.FalseLabel);
                if (c.Then is not null) foreach (var s in c.Then) CollectReferences(s, into);
                break;
            case OverflowIfStmt o:
                into.Add(o.Label);
                if (o.Then is not null) foreach (var s in o.Then) CollectReferences(s, into);
                break;
            case BattleStmt b:
                if (b.WinAction == 2) into.Add(b.WinLabel);
                if (b.LoseAction == 2) into.Add(b.LoseLabel);
                break;
            case CallStmt call:
                var spec = OpcodeTable.Get(call.Opcode);
                for (int k = 0; k < call.Args.Length; k++)
                    if (spec.ArgAt(k).Kind == ArgKind.Label) into.Add(call.Args[k]);
                break;
            case InitStmt init:
                foreach (var s in init.Body) CollectReferences(s, into);
                break;
        }
    }

    private static void ThrowIfUnrepresentable(ScriptInstruction instr)
    {
        long expected = 8L + 4 * instr.RawParams.Length;
        if (instr.Size != expected)
            throw new FormatException(
                $"opcode {instr.BaseOpcode} has size {instr.Size} with {instr.RawParams.Length} argument words; " +
                "BDSL can only express a size of 8 + 4 words");
        int words = OpcodeTable.Get(instr.BaseOpcode).Words;
        if (instr.RawParams.Length < words)
            throw new FormatException(
                $"opcode {instr.BaseOpcode} has {instr.RawParams.Length} argument words but the opcode table lists " +
                $"{words}; BDSL cannot express an instruction shorter than its opcode");
    }

    private static Statement FromInstruction(ScriptInstruction instr)
    {
        uint op = instr.BaseOpcode;
        bool disabled = instr.Disabled;
        uint[] a = instr.RawParams;
        var fallback = new CallStmt(op, a, disabled);
        if (a.Length != OpcodeTable.Get(op).Words) return fallback;

        switch (op)
        {
            case OpcodeTable.Label:
                return a[1] == 0 ? new LabelStmt(a[0], disabled) : fallback;
            case OpcodeTable.GotoLabel:
                return a[1] == 0 ? new GotoStmt(a[0], disabled) : fallback;
            case OpcodeTable.EndScript:
                return a[0] == 0 && a[1] == 0 ? new EndStmt(disabled) : fallback;
            case OpcodeTable.Comment:
                return !disabled && CommentText.TryDecode(a, out var text) ? new CommentStmt(text) : fallback;
            case OpcodeTable.SetVariable:
                return a[4] == 0 && a[5] == 0 && a[1] <= 4 && (a[2] <= 9 || a[2] == 100)
                    && (a[2] <= 3 || a[3] == 0)
                    ? new AssignStmt(a, disabled) : fallback;
            case OpcodeTable.FlagGetSet:
                return a[3] == 0 && a[4] == 0 && a[5] == 0 && a[1] <= 1 ? new FlagStmt(a, disabled) : fallback;
            case OpcodeTable.IfExtended:
                return a[9] == 0 && ActionRenderable(a[5], a[6]) && ActionRenderable(a[7], a[8])
                    && ConditionRenderable(a[0], a[1], a[4]) && ConditionComplete(a[0], a[2], a[3])
                    ? new IfStmt(a, disabled, null) : fallback;
            case OpcodeTable.Overflow:
                return a[5] == 0 && a[0] <= 2 && (a[4] & ~6u) == 0
                    && (a[0] == 0 || (a[1] == 0 && (a[4] & 2) == 0))
                    ? new OverflowIfStmt(a, disabled, null) : fallback;
            case OpcodeTable.Battle:
                return ActionRenderable(a[5], a[6]) && ActionRenderable(a[7], a[8])
                    ? new BattleStmt(a, disabled) : fallback;
            default:
                return fallback;
        }
    }

    private static bool ActionRenderable(uint action, uint label) => action switch
    {
        0 or 1 => label == 0,
        2 => true,
        _ => false,
    };

    private static bool ConditionComplete(uint type, uint operand, uint compareValue) => type switch
    {
        2 => compareValue == 0,
        3 or 4 or 5 or 6 => operand == 0,
        _ => true,
    };

    private static bool ConditionRenderable(uint type, uint sub, uint op) => type switch
    {
        0 => sub <= 1 && op <= 5,
        1 => sub <= 1 && op <= 1,
        2 => sub == 0 && op <= 1,
        3 or 5 or 6 => sub == 0 && op <= 5,
        4 => sub <= 1 && op <= 5,
        _ => false,
    };

    private static List<Statement> GroupInit(List<Statement> flat)
    {
        var result = new List<Statement>(flat.Count);
        for (int i = 0; i < flat.Count; i++)
        {
            if (flat[i] is CallStmt { Opcode: OpcodeTable.InitBegin, Disabled: false })
            {
                int end = FindInitEnd(flat, i + 1);
                if (end >= 0)
                {
                    result.Add(new InitStmt(flat.GetRange(i + 1, end - i - 1)));
                    i = end;
                    continue;
                }
            }
            result.Add(flat[i]);
        }
        return result;
    }

    private static int FindInitEnd(List<Statement> flat, int from)
    {
        for (int j = from; j < flat.Count; j++)
        {
            if (flat[j] is CallStmt { Opcode: OpcodeTable.InitBegin, Disabled: false }) return -1;
            if (flat[j] is CallStmt { Opcode: OpcodeTable.InitEnd, Disabled: false }) return j;
        }
        return -1;
    }
}
