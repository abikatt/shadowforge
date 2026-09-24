namespace ShadowForge.Formats.EVT;

/// <summary>
/// One EVT file. The game walks the file image in place, so these header offsets are
/// also the in-memory layout.
/// </summary>
public sealed class EventScene
{
    public const int HeaderSize = 0x2C;

    public const int VersionOffset = 0x10;
    public const int ChecksumOffset = 0x14;
    public const int WidthOffset = 0x18;
    public const int HeightOffset = 0x1C;
    public const int ScaleOffset = 0x20;
    public const int FramesOffset = 0x24;
    public const int TrackCountOffset = 0x28;

    public string Name = "";
    public uint Version;
    public uint Checksum;
    public int Width;
    public int Height;
    public float Scale;
    public int Frames;
    public List<EventTrack> Tracks = [];
}
