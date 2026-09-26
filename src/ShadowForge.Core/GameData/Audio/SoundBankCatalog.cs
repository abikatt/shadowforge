using ShadowForge.Formats.XACT;

namespace ShadowForge.GameData.Audio;

/// <summary>
/// One wave bank on disk. VfsPath is relative to the game-data root
/// (snd_stream_us\voice\ev001_us.xwb), which is also where a mod overrides it. Folder is the
/// top snd_* folder, which says whether the bank is streamed or held in memory and its
/// language.
/// </summary>
public sealed record SoundBankEntry(string VfsPath, string Folder, string Kind, string Name, long Size);

/// <summary>
/// One wave of a bank, with the cues that play it from the bank's sound bank (.xsb).
/// </summary>
public sealed record SoundWave(int Index, WaveFormat Format, double Seconds, IReadOnlyList<string> Cues);

/// <summary>
/// Lists the game's wave banks: every .xwb under the snd_* folders of the game-data root, where
/// both loose extracts and reblue installs keep them.
/// </summary>
public sealed class SoundBankCatalog
{
    private readonly GameInstall _install;

    public SoundBankCatalog(GameInstall install) => _install = install;

    public IReadOnlyList<SoundBankEntry> List()
    {
        var banks = new List<SoundBankEntry>();
        foreach (string folder in Directory.EnumerateDirectories(_install.GameDataRoot, "snd_*"))
        {
            foreach (string file in Directory.EnumerateFiles(folder, "*.xwb", SearchOption.AllDirectories))
            {
                string vfs = Path.GetRelativePath(_install.GameDataRoot, file).Replace('/', '\\');
                string[] parts = vfs.Split('\\');
                banks.Add(new SoundBankEntry(vfs, parts[0], parts.Length > 2 ? parts[1] : "",
                    Path.GetFileNameWithoutExtension(file), new FileInfo(file).Length));
            }
        }
        banks.Sort((a, b) => string.Compare(a.VfsPath, b.VfsPath, StringComparison.OrdinalIgnoreCase));
        return banks;
    }

    public string FullPath(SoundBankEntry bank) => Path.Combine(_install.GameDataRoot, bank.VfsPath);

    /// <summary>
    /// The bank's waves, named by the cues of the .xsb beside it that play from this bank (a
    /// complex cue by its first clip). A bank without a readable .xsb lists its waves unnamed.
    /// </summary>
    public static IReadOnlyList<SoundWave> Waves(WaveBank bank, string wavePath)
    {
        var names = new Dictionary<int, List<string>>();
        string xsb = Path.ChangeExtension(wavePath, ".xsb");
        if (File.Exists(xsb))
        {
            try
            {
                var sounds = SoundBank.ReadFile(xsb);
                int self = sounds.WaveBankNames.FindIndex(n =>
                    n.Equals(Path.GetFileNameWithoutExtension(wavePath), StringComparison.OrdinalIgnoreCase));
                foreach (var cue in sounds.Cues.Where(c => c.WaveBankIndex == Math.Max(self, 0)))
                {
                    if (!names.TryGetValue(cue.WaveIndex, out var list)) names[cue.WaveIndex] = list = [];
                    list.Add(cue.Name);
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentException)
            {
            }
        }

        return bank.Entries.Select((e, i) => new SoundWave(i, e.Format,
                e.Format.SamplesPerSecond > 0 ? (double)e.DurationSamples / e.Format.SamplesPerSecond : 0,
                names.TryGetValue(i, out var cues) ? cues : []))
            .ToList();
    }
}
