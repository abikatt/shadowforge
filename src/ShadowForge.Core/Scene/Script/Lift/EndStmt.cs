namespace ShadowForge.Scene.Script.Lift;

/// <summary>
/// Opcode 5023.
/// </summary>
public sealed record EndStmt(bool Disabled) : Statement(Disabled);
