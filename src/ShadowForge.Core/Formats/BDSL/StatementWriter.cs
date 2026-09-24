using System.Text;
using ShadowForge.Scene.Script;
using ShadowForge.Scene.Script.Lift;

namespace ShadowForge.Formats.BDSL;

/// <summary>
/// Writes statement trees as when-block body text.
/// </summary>
public static class StatementWriter
{
    /// <summary>
    /// One statement per line at the given indent. Block bodies indent four more.
    /// </summary>
    public static void Write(StringBuilder sb, IReadOnlyList<Statement> statements, int indent)
    {
        string pad = new(' ', indent);
        foreach (var stmt in statements)
        {
            switch (stmt)
            {
                case RawStmt raw:
                    sb.Append(pad).Append("raw ").AppendLine(Convert.ToHexString(raw.Bytes));
                    break;
                case CommentStmt comment:
                    sb.Append(pad).Append("// ").AppendLine(comment.Text);
                    break;
                case InitStmt init:
                    sb.Append(pad).AppendLine("init {");
                    Write(sb, init.Body, indent + 4);
                    sb.Append(pad).AppendLine("}");
                    break;
                case IfStmt { Then: not null } cond:
                    sb.Append(pad).Append("if ").Append(Condition(cond)).AppendLine(" {");
                    Write(sb, cond.Then, indent + 4);
                    sb.Append(pad).AppendLine("}");
                    break;
                case OverflowIfStmt { Then: not null } overflow:
                    sb.Append(pad).Append("if !").Append(OverflowCondition(overflow)).AppendLine(" {");
                    Write(sb, overflow.Then, indent + 4);
                    sb.Append(pad).AppendLine("}");
                    break;
                default:
                    sb.Append(pad);
                    if (stmt.Disabled) sb.Append('!');
                    sb.AppendLine(Line(stmt));
                    break;
            }
        }
    }

    private static string Line(Statement stmt) => stmt switch
    {
        CallStmt call => Call(OpcodeTable.Get(call.Opcode), call.Args, null),
        LabelStmt label => $"@{label.Id}:",
        GotoStmt go => $"goto @{go.Label}",
        EndStmt => "end",
        AssignStmt assign => $"var[{assign.Dest}] {StatementSyntax.AssignOps[assign.Op]} {AssignValue(assign)}",
        FlagStmt { IsGet: true } flag => $"var[{flag.ValueOrDest}] = flag[{flag.Flag}]",
        FlagStmt flag => $"flag[{flag.Flag}] = {Int(flag.ValueOrDest)}",
        IfStmt cond => "if " + Condition(cond) + Actions(cond.TrueAction, cond.TrueLabel, cond.FalseAction, cond.FalseLabel),
        OverflowIfStmt overflow => "if " + OverflowCondition(overflow) + $" then goto @{overflow.Label}",
        BattleStmt battle => Call(OpcodeTable.Get(OpcodeTable.Battle), battle.Args, [5, 6, 7, 8])
                             + BattleActions(battle),
        _ => throw new InvalidOperationException($"unknown statement {stmt.GetType().Name}"),
    };

    private static string Call(OpcodeSpec spec, uint[] args, int[]? skip)
    {
        var parts = new List<string>();
        int pinned = args.Length == spec.Words ? -1 : args.Length - 1;
        for (int i = 0; i < args.Length; i++)
        {
            if (skip is not null && Array.IndexOf(skip, i) >= 0) continue;
            if (args[i] == 0 && i != pinned) continue;
            var arg = spec.ArgAt(i);
            parts.Add($"{arg.Name}={ValueFormat.Format(args[i], arg)}");
        }
        return $"{spec.Name}({string.Join(", ", parts)})";
    }

    private static string AssignValue(AssignStmt a) => a.ValueType switch
    {
        0 => Int(a.Value),
        1 => $"var[{a.Value}]",
        2 => $"random({Int(a.Value >> 16)}, {Int(a.Value & 0xFFFF)})",
        3 => $"chara({ValueFormat.Format(a.Value >> 16, ValueFormat.Character)}, {Int(a.Value & 0xFFFF)})",
        var type when StatementSyntax.ValueSources.TryGetName(type, out var name) => name,
        _ => throw new InvalidOperationException($"value_type {a.ValueType} is not liftable"),
    };

    private static string Int(uint value) => ValueFormat.Format(value, ValueFormat.PlainInt);

    private static string Condition(IfStmt c)
    {
        string op = CompareOp.Name(c.CompareOp);
        string rhs = Int(c.CompareValue);
        return c.CheckType switch
        {
            0 => $"var[{c.Operand}] {op} " + (c.SubType == 1 ? $"var[{c.CompareValue}]" : rhs),
            1 => $"{(c.SubType == 1 ? "inventory_count" : "item_count")}({Int(c.Operand)}) {(c.CompareOp == 0 ? "<" : ">=")} {rhs}",
            2 => (c.CompareOp == 0 ? "!" : "") + $"in_party({ValueFormat.Format(c.Operand, ValueFormat.Character)})",
            3 => $"gold {op} {rhs}",
            4 => $"{(c.SubType == 1 ? "realtime" : "playtime")} {op} {rhs}",
            5 => $"medals {op} {rhs}",
            6 => $"chapter {op} {rhs}",
            _ => throw new InvalidOperationException($"check_type {c.CheckType} is not liftable"),
        };
    }

    private static string OverflowCondition(OverflowIfStmt o)
    {
        string amount = (o.Flags & 4) != 0 ? $"var[{o.Amount}]" : Int(o.Amount);
        string subject = o.CheckType switch
        {
            0 => "item " + ((o.Flags & 2) != 0 ? $"var[{o.Item}]" : Int(o.Item)),
            1 => "gold",
            _ => "medals",
        };
        return $"overflow({subject}, +{amount})";
    }

    private static string Actions(uint trueAction, uint trueLabel, uint falseAction, uint falseLabel)
    {
        var sb = new StringBuilder();
        if (trueAction != 0) sb.Append(" then ").Append(Action(trueAction, trueLabel));
        if (falseAction != 0) sb.Append(" else ").Append(Action(falseAction, falseLabel));
        return sb.ToString();
    }

    private static string BattleActions(BattleStmt b)
    {
        var sb = new StringBuilder();
        if (b.WinAction != 0) sb.Append(" on_win ").Append(Action(b.WinAction, b.WinLabel));
        if (b.LoseAction != 0) sb.Append(" on_lose ").Append(Action(b.LoseAction, b.LoseLabel));
        return sb.ToString();
    }

    private static string Action(uint action, uint label) => action == 1 ? "end" : $"goto @{label}";
}
