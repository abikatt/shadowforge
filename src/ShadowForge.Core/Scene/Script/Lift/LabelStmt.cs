namespace ShadowForge.Scene.Script.Lift;

/// <summary>
/// Opcode 5000.
/// </summary>
public sealed record LabelStmt(uint Id, bool Disabled) : Statement(Disabled);
