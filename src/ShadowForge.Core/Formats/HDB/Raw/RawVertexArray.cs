namespace ShadowForge.Formats.HDB.Raw;

/// <summary>
/// One vertex block: a 16-byte header and undecoded vertex bytes.
/// </summary>
public sealed class RawVertexArray
{
    public uint ByteSize { get; set; }
    public uint FormatType { get; set; }
    public uint VertexCount { get; set; }
    public uint Reserved0C { get; set; }
    public byte[] RawBytes { get; set; } = [];
}
