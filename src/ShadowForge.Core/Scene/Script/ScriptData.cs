namespace ShadowForge.Scene.Script;

/// <summary>
/// Bytecode that does not decode as an instruction, kept verbatim.
/// </summary>
public sealed class ScriptData : ScriptElement
{
    public byte[] Bytes { get; set; } = [];
}
