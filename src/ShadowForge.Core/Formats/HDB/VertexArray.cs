namespace ShadowForge.Formats.HDB;

public sealed class VertexArray
{
    /// <summary>
    /// Format word from the VA block header.
    /// </summary>
    public uint VAType { get; set; }
    public int VASize { get; set; }
    public int VertexCount { get; set; }
    public byte[] RawVertices { get; set; } = [];
}
