namespace ShadowForge.Formats.HDB.Raw;

public sealed class RawVASetupRecord
{
    public uint VertexCount { get; set; }
    public uint FormatType { get; set; }
    public uint Offset { get; set; }
}
