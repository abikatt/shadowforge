namespace ShadowForge.Formats.EVT;

/// <summary>
/// The keys one track fires at one frame.
/// </summary>
public sealed class EventEntry
{
    /// <summary>
    /// Bytes of the record that precedes the entry's key run.
    /// </summary>
    public const int RecordSize = 72;

    public const int FrameOffset = 0;
    public const int NameOffset = 4;
    public const int KeysLengthOffset = 68;

    public int Frame;
    public string Name = "";
    public List<EventKey> Keys = [];
}
