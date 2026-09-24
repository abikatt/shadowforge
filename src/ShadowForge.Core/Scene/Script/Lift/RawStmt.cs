namespace ShadowForge.Scene.Script.Lift;

/// <summary>
/// Bytes that are not an instruction, kept verbatim.
/// </summary>
public sealed record RawStmt(byte[] Bytes) : Statement(false);
