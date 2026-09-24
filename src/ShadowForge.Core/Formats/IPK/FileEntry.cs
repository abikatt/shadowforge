namespace ShadowForge.Formats.IPK;

public sealed class FileEntry
{
    public string Name { get; set; } = "";
    public bool IsCompressed { get; set; }
    public uint CompressedSize { get; set; }
    public uint Offset { get; set; }
    public uint OriginalSize { get; set; }
}
