namespace ShadowForge.Scene.Script.Lift;

/// <summary>
/// Object init section between opcodes 5057 and 5058.
/// </summary>
public sealed record InitStmt(IReadOnlyList<Statement> Body) : Statement(false);
