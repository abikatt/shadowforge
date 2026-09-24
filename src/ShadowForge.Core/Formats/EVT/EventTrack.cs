namespace ShadowForge.Formats.EVT;

public sealed class EventTrack
{
    /// <summary>
    /// Bytes of the record that precedes the track's entries.
    /// </summary>
    public const int RecordSize = 144;

    public const int TypeOffset = 0;
    public const int EntryCountOffset = 4;
    public const int NameOffset = 8;
    public const int LabelOffset = 72;
    public const int FlagsOffset = 136;
    public const int ExtraOffset = 140;

    /// <summary>
    /// Width of the NUL-padded name and label fields.
    /// </summary>
    public const int NameFieldWidth = 64;

    public int Type;
    public string Name = "";
    public string Label = "";
    public uint Flags;
    public uint Extra;
    public List<EventEntry> Entries = [];

    public EventTrackKind Kind => (EventTrackKind)Type;

    /// <summary>
    /// An object track whose label starts with pc, ch or sw is a character actor, the same
    /// prefix test the game applies to an attach target. Any other object track is a prop
    /// or, for a .map name, the stage.
    /// </summary>
    public bool IsActor =>
        Type == (int)EventTrackKind.Object &&
        (Label.StartsWith("pc", StringComparison.Ordinal) ||
         Label.StartsWith("ch", StringComparison.Ordinal) ||
         Label.StartsWith("sw", StringComparison.Ordinal));
}
