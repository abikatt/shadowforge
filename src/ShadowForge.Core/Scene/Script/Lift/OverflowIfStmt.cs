namespace ShadowForge.Scene.Script.Lift;

/// <summary>
/// Capacity overflow check, opcode 5042. Then is set for the structured form.
/// </summary>
public sealed record OverflowIfStmt(uint[] Args, bool Disabled, IReadOnlyList<Statement>? Then) : Statement(Disabled)
{
    public uint CheckType => Args[0];
    public uint Item => Args[1];
    public uint Amount => Args[2];
    public uint Label => Args[3];
    public uint Flags => Args[4];
}
