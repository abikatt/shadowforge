namespace ShadowForge.Scene.Script.Lift;

/// <summary>
/// Extended conditional, opcode 5012. Then is set for the structured form.
/// </summary>
public sealed record IfStmt(uint[] Args, bool Disabled, IReadOnlyList<Statement>? Then) : Statement(Disabled)
{
    public uint CheckType => Args[0];
    public uint SubType => Args[1];
    public uint Operand => Args[2];
    public uint CompareValue => Args[3];
    public uint CompareOp => Args[4];
    public uint TrueAction => Args[5];
    public uint TrueLabel => Args[6];
    public uint FalseAction => Args[7];
    public uint FalseLabel => Args[8];
}
