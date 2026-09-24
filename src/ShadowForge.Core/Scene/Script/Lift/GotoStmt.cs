namespace ShadowForge.Scene.Script.Lift;

/// <summary>
/// Opcode 5013.
/// </summary>
public sealed record GotoStmt(uint Label, bool Disabled) : Statement(Disabled);
