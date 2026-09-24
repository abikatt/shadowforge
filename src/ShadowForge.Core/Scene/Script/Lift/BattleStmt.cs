namespace ShadowForge.Scene.Script.Lift;

/// <summary>
/// Battle encounter, opcode 5016.
/// </summary>
public sealed record BattleStmt(uint[] Args, bool Disabled) : Statement(Disabled)
{
    public uint WinAction => Args[5];
    public uint WinLabel => Args[6];
    public uint LoseAction => Args[7];
    public uint LoseLabel => Args[8];
}
