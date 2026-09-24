namespace ShadowForge.Formats.XACT;

public enum SeResidency
{
    InMemory,
    Streaming,
}

/// <summary>
/// selist.csv, the Shift-JIS table naming every sound effect. Columns are bank base name,
/// cue name, residency and reverb preset, then Japanese comments. The engine indexes the
/// table by row, so an SE id is the row's zero-based position after the header. The
/// engine's play function adds <see cref="PlayIdBase"/> to reach ids at or above 100.
/// </summary>
public sealed class SeList
{
    public const string HeaderPrefix = "#BANK";

    public const int PlayIdBase = 100;

    public const string SystemBank = "sys";
    public const string MemoryDirectory = "snd_memory";
    public const string StreamDirectory = "snd_stream";

    /// <summary>Suffix on the localized bank directory and bank file names.</summary>
    public const string LocalizedSuffix = "_us";

    public const int BankColumn = 0;
    public const int CueColumn = 1;
    public const int ResidencyColumn = 2;
    public const int ReverbColumn = 3;

    public const string StreamingKeyword = "STREAMING";

    /// <summary>
    /// Indexed by SE id, blank rows included.
    /// </summary>
    public List<SeEntry> Entries { get; } = [];

    /// <summary>Holds selist.csv and the sys bank.</summary>
    public static string SystemSoundDirectory => Path.Combine("!necessity", "bd_system", "sound");

    public static string DefaultPath(string gameDataRoot)
        => Path.Combine(gameDataRoot, SystemSoundDirectory, "selist.csv");

    /// <summary>
    /// The first line is the header the engine skips. Every line after it is a row, numbered from zero.
    /// </summary>
    public static SeList Parse(byte[] data)
    {
        string text = EncodingExtensions.DecodeShiftJISRaw(data, 0, data.Length);
        var lines = text.Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);

        var list = new SeList();
        for (int line = 1; line < lines.Count; line++)
        {
            string row = lines[line].TrimEnd('\r');
            var entry = new SeEntry { Id = line - 1 };
            if (row.Trim().Length != 0)
            {
                var columns = row.Split(',');
                entry.Bank = Column(columns, BankColumn);
                entry.Cue = Column(columns, CueColumn);
                entry.Residency = string.Equals(Column(columns, ResidencyColumn), StreamingKeyword,
                                                StringComparison.OrdinalIgnoreCase)
                    ? SeResidency.Streaming
                    : SeResidency.InMemory;
                entry.Reverb = Column(columns, ReverbColumn);
            }
            list.Entries.Add(entry);
        }
        return list;
    }

    public static SeList ReadFile(string path) => Parse(File.ReadAllBytes(path));

    /// <summary>The row with this SE id, or null when it is out of range or blank.</summary>
    public SeEntry? Find(int id)
        => id >= 0 && id < Entries.Count && !Entries[id].IsBlank ? Entries[id] : null;

    /// <summary>
    /// Cue names repeat across the table, so this returns every matching row.
    /// </summary>
    public IReadOnlyList<SeEntry> FindByCue(string cue)
        => Entries.Where(e => string.Equals(e.Cue, cue, StringComparison.OrdinalIgnoreCase)).ToList();

    private static string Column(string[] columns, int index)
        => index < columns.Length ? columns[index].Trim() : "";
}
