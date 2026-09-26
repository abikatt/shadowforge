using ShadowForge.Formats.XACT;
using ShadowForge.GameData.Audio;
using ShadowForge.GameData.Mods;

namespace ShadowForge.Workbench;

/// <summary>
/// A replacement saved for one wave: lossless 16-bit PCM (.wav) or already encoded XMA (.xma).
/// </summary>
public sealed record WaveReplacement(int Index, string Path)
{
    public bool IsXma => System.IO.Path.GetExtension(Path).Equals(AudioWorkspace.XmaExtension, StringComparison.OrdinalIgnoreCase);
}

public sealed record AudioBuildResult(string BankPath, int Replaced, int Encoded, long SizeBefore, long SizeAfter);

/// <summary>
/// Replacement waves kept under a working folder that mirrors the game's layout, one folder per
/// bank ({root}\snd_stream_us\voice\ev001_us\012.wav), and their build into a mod at the path
/// the game reads. A replacement is saved lossless as .wav or lossy as .xma; the build always
/// writes XMA, encoding the .wav ones, so a mod carries banks shaped like the game's own. The
/// build starts from the game's bank each time, so the working folder alone says what the mod
/// changes.
/// </summary>
public sealed class AudioWorkspace
{
    public const string WavExtension = ".wav";
    public const string XmaExtension = ".xma";

    public string Root { get; }

    public AudioWorkspace(string root) => Root = root;

    public string DirFor(SoundBankEntry bank) =>
        Path.Combine(Root, Path.ChangeExtension(bank.VfsPath, null));

    public string PathFor(SoundBankEntry bank, int index, bool xma) =>
        Path.Combine(DirFor(bank), $"{index:D3}{(xma ? XmaExtension : WavExtension)}");

    /// <summary>
    /// The saved replacements of a bank by wave index. A wave with both forms on disk uses the
    /// newer one.
    /// </summary>
    public IReadOnlyDictionary<int, WaveReplacement> Replacements(SoundBankEntry bank)
    {
        var found = new Dictionary<int, WaveReplacement>();
        string dir = DirFor(bank);
        if (!Directory.Exists(dir)) return found;
        foreach (string file in Directory.EnumerateFiles(dir).OrderBy(File.GetLastWriteTimeUtc))
        {
            string ext = Path.GetExtension(file);
            if (!ext.Equals(WavExtension, StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(XmaExtension, StringComparison.OrdinalIgnoreCase)) continue;
            if (int.TryParse(Path.GetFileNameWithoutExtension(file), out int index) && index >= 0)
                found[index] = new WaveReplacement(index, file);
        }
        return found;
    }

    /// <summary>
    /// Saves a replacement, as XMA when an encoder is given and as lossless .wav otherwise,
    /// removing the wave's replacement in the other form.
    /// </summary>
    public WaveReplacement Save(SoundBankEntry bank, int index, WavFile wav, string? xmaEncoder)
    {
        bool xma = xmaEncoder is not null;
        string path = PathFor(bank, index, xma);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (xma) File.WriteAllBytes(path, AudioTools.EncodeXma(wav, xmaEncoder!));
        else wav.WriteFile(path);

        string other = PathFor(bank, index, !xma);
        if (File.Exists(other)) File.Delete(other);
        return new WaveReplacement(index, path);
    }

    public void Revert(SoundBankEntry bank, int index)
    {
        foreach (bool xma in new[] { false, true })
        {
            string path = PathFor(bank, index, xma);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>
    /// The replacement as a bank entry, for playing or exporting it.
    /// </summary>
    public static WaveBankEntry LoadEntry(WaveReplacement replacement)
    {
        if (replacement.IsXma) return XmaFile.ReadFile(replacement.Path).ToEntry();
        var wav = WavFile.ReadFile(replacement.Path);
        return new WaveBankEntry
        {
            Format = WaveFormat.Pcm16(wav.Channels, wav.SampleRate),
            Data = WavFile.SwapSamples(wav.Samples),
            DurationSamples = (uint)wav.FrameCount,
        };
    }

    /// <summary>
    /// Whether building needs the XMA encoder, that is whether a replacement is still lossless.
    /// </summary>
    public bool NeedsEncoder(SoundBankEntry bank) => Replacements(bank).Values.Any(r => !r.IsXma);

    /// <summary>
    /// Whether the mod's copy of the bank is at least as new as every replacement and there is
    /// one exactly when there are replacements.
    /// </summary>
    public bool IsBuilt(SoundBankEntry bank, string modsRoot, string modName)
    {
        var replacements = Replacements(bank);
        string modPath = ModBankPath(bank, modsRoot, modName);
        if (!File.Exists(modPath)) return replacements.Count == 0;
        DateTime built = File.GetLastWriteTimeUtc(modPath);
        return replacements.Count > 0 && replacements.Values.All(r => File.GetLastWriteTimeUtc(r.Path) <= built);
    }

    public static string ModBankPath(SoundBankEntry bank, string modsRoot, string modName) =>
        Path.Combine(modsRoot, modName, bank.VfsPath);

    /// <summary>
    /// Writes the game's bank with every replacement into the mod, all as XMA, adding a
    /// mod.toml when the mod has none. With no replacements left, the mod's copy is removed so
    /// the game's bank plays again. Lossless replacements need the encoder.
    /// </summary>
    public AudioBuildResult Build(SoundBankEntry bank, string gamePath, string modsRoot, string modName, string? xmaEncoder)
    {
        var replacements = Replacements(bank);
        string modPath = ModBankPath(bank, modsRoot, modName);
        long sizeBefore = new FileInfo(gamePath).Length;
        if (replacements.Count == 0)
        {
            if (File.Exists(modPath)) File.Delete(modPath);
            return new AudioBuildResult(modPath, 0, 0, sizeBefore, sizeBefore);
        }
        if (xmaEncoder is null && replacements.Values.Any(r => !r.IsXma))
            throw new InvalidOperationException(
                "Building lossless replacements into a mod encodes them as XMA, which needs the XMA encoder. Set it in Settings.");

        var target = WaveBank.ReadFile(gamePath);
        int encoded = 0;
        foreach (var replacement in replacements.Values.OrderBy(r => r.Index))
        {
            if (replacement.Index >= target.Entries.Count)
                throw new InvalidDataException(
                    $"{Path.GetFileName(replacement.Path)} names wave {replacement.Index}, but {bank.Name} has {target.Entries.Count}.");
            byte[] xma;
            if (replacement.IsXma)
            {
                xma = File.ReadAllBytes(replacement.Path);
            }
            else
            {
                xma = AudioTools.EncodeXma(WavFile.ReadFile(replacement.Path), xmaEncoder!);
                encoded++;
            }
            target.ReplaceEntry(replacement.Index, XmaFile.Read(xma, Path.GetFileName(replacement.Path)));
        }

        byte[] written = target.Write();
        Directory.CreateDirectory(Path.GetDirectoryName(modPath)!);
        File.WriteAllBytes(modPath, written);

        string toml = Path.Combine(modsRoot, modName, "mod.toml");
        if (!File.Exists(toml))
            ModDeployer.WriteToml(toml, new ModMetadata(modName, null, null, "Audio replacements"));
        return new AudioBuildResult(modPath, replacements.Count, encoded, sizeBefore, written.Length);
    }
}
