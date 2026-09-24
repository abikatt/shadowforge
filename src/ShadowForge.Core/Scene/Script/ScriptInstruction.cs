namespace ShadowForge.Scene.Script;

public sealed class ScriptInstruction : ScriptElement
{
    /// <summary>
    /// Includes <see cref="OpcodeTable.DisabledFlag"/>.
    /// </summary>
    public uint Opcode { get; set; }

    public uint BaseOpcode => Opcode & ~OpcodeTable.DisabledFlag;

    public bool Disabled => (Opcode & OpcodeTable.DisabledFlag) != 0;

    /// <summary>
    /// Bytes including the 8-byte opcode and size header.
    /// </summary>
    public uint Size { get; set; }

    public uint[] RawParams { get; set; } = [];
}
