namespace ShadowForge.Scene.Script.Lift;

/// <summary>
/// Variable assignment, opcode 5003.
/// </summary>
public sealed record AssignStmt(uint[] Args, bool Disabled) : Statement(Disabled)
{
    public uint Dest => Args[0];
    public uint Op => Args[1];
    public uint ValueType => Args[2];
    public uint Value => Args[3];
}
