namespace ShadowForge.Formats.XACT;

/// <summary>
/// One row of selist.csv: the cue the game plays and the bank holding it.
/// </summary>
public sealed class SeEntry
{
    /// <summary>
    /// The zero-based data row index. For a 1-based line number counting the header line
    /// this is lineNumber - 2.
    /// </summary>
    public int Id { get; set; }

    /// <summary>Bank base name, without directory or extension.</summary>
    public string Bank { get; set; } = "";

    public string Cue { get; set; } = "";

    public SeResidency Residency { get; set; }

    /// <summary>Reverb preset name, empty when the row names none.</summary>
    public string Reverb { get; set; } = "";

    public bool IsStreaming => Residency == SeResidency.Streaming;

    /// <summary>
    /// A blank line in the table, kept so every later row's id still matches its line.
    /// </summary>
    public bool IsBlank => Bank.Length == 0 && Cue.Length == 0;

    /// <summary>
    /// Bank path relative to the game-data root for ".xsb" or ".xwb". The sys bank is the
    /// exception: the engine loads it from the system sound directory at startup, so it is
    /// always resident.
    /// </summary>
    public string BankRelativePath(string extension)
    {
        if (IsSystemBank)
            return Path.Combine(SeList.SystemSoundDirectory, Bank + extension);
        return Path.Combine(ResidencyDirectory, "se", Bank + extension);
    }

    public string SoundBankRelativePath => BankRelativePath(".xsb");

    public string WaveBankRelativePath => BankRelativePath(".xwb");

    /// <summary>
    /// The bank file under a game-data root, or null when it is absent. Localized banks ship
    /// beside the base ones as "{dir}_us/se/{bank}_us", which is probed second.
    /// </summary>
    public string? ResolveBankPath(string gameDataRoot, string extension)
    {
        string primary = Path.Combine(gameDataRoot, BankRelativePath(extension));
        if (File.Exists(primary)) return primary;

        if (IsSystemBank)
            return null;

        string localized = Path.Combine(
            gameDataRoot, ResidencyDirectory + SeList.LocalizedSuffix, "se", Bank + SeList.LocalizedSuffix + extension);
        return File.Exists(localized) ? localized : null;
    }

    private bool IsSystemBank => string.Equals(Bank, SeList.SystemBank, StringComparison.OrdinalIgnoreCase);

    private string ResidencyDirectory => IsStreaming ? SeList.StreamDirectory : SeList.MemoryDirectory;
}
