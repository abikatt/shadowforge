namespace ShadowForge.Formats.HDB;

public sealed class RenderCommand
{
    public byte Opcode { get; set; }
    public byte[] Data { get; set; } = [];
}
