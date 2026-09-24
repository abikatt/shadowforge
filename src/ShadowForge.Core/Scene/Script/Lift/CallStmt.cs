namespace ShadowForge.Scene.Script.Lift;

/// <summary>
/// Any opcode with its raw argument words.
/// </summary>
public sealed record CallStmt(uint Opcode, uint[] Args, bool Disabled) : Statement(Disabled);
