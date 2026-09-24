namespace ShadowForge.Scene.Script.Lift;

/// <summary>
/// Game flag read or write, opcode 5063.
/// </summary>
public sealed record FlagStmt(uint[] Args, bool Disabled) : Statement(Disabled)
{
    public uint Flag => Args[0];
    public bool IsGet => Args[1] == 1;
    public uint ValueOrDest => Args[2];
}
