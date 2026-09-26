using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ShadowForge.Formats.XACT;
using ShadowForge.GameData.Audio;

namespace ShadowForge.Workbench;

public sealed record BankRow(SoundBankEntry Entry)
{
    public string Name => Entry.Name;
    public string Kind => Entry.Kind;
    public string Detail => $"{Entry.Folder} · {Entry.Size / 1024.0 / 1024.0:0.0} MB";
}

public sealed record WaveRow(SoundWave Wave, WaveReplacement? Replacement)
{
    public bool IsReplaced => Replacement is not null;

    public string Status => Replacement switch
    {
        null => "",
        { IsXma: true } => "replaced · XMA",
        _ => "replaced · lossless",
    };

    public string Title => $"#{Wave.Index:D3}" + (Wave.Cues.Count > 0 ? "  " + string.Join(", ", Wave.Cues) : "");

    public string Detail
    {
        get
        {
            var f = Wave.Format;
            string channels = f.Channels switch { 1 => "mono", 2 => "stereo", _ => $"{f.Channels} ch" };
            var time = TimeSpan.FromSeconds(Wave.Seconds);
            return $"{(int)time.TotalMinutes}:{time.Seconds:00}.{time.Milliseconds / 100} · {f.Tag} {channels} {f.SamplesPerSecond / 1000.0:0.#} kHz";
        }
    }
}

/// <summary>
/// The Audio tab: the game's wave banks, whose waves can be played, exported as .wav and
/// replaced. A replacement is saved to the audio replacements folder, lossless or as XMA, and
/// Build into mod writes the bank with all of them into the mod, always as XMA.
/// </summary>
public partial class MainWindow
{
    private const string AllFolders = "All folders";

    private const string NeedsXmaEncoder =
        "needs the XMA encoder (xmaencode.exe from the Xbox 360 SDK), which cannot be bundled with ShadowForge. "
        + "Point to it in Settings.";

    private SoundBankCatalog? _soundCatalog;
    private IReadOnlyList<BankRow> _banks = [];
    private BankRow? _selectedBank;
    private WaveBank? _gameBank;

    private string? Ffmpeg => AudioTools.FindFfmpeg(_settings.FfmpegPath);

    private string? XmaEncoder => AudioTools.FindXmaEncoder(_settings.XmaEncoderPath);

    private AudioWorkspace AudioWork => new(_settings.AudioWorkRoot);

    private void SetUpAudio()
    {
        BankList.SelectionChanged += (_, _) =>
        {
            if (!_refreshingList) ShowBankDetails(BankList.SelectedItem as BankRow);
        };
        SetFilterChoices(BankFolderBox, AllFolders, [], null);
        BankFolderBox.SelectionChanged += (_, _) =>
        {
            if (_updatingListOptions) return;
            _settings.AudioFolder = FilterValue(BankFolderBox);
            _settings.Save();
            ApplyFilter();
        };
        AudioModName.Text = _settings.AudioModName;
        AudioEnableOnBuild.IsChecked = _settings.AudioEnableOnBuild;
        AudioModName.LostFocus += (_, _) =>
        {
            string name = AudioModName.Text?.Trim() ?? "";
            if (name == _settings.AudioModName || !IsValidModName(name)) return;
            _settings.AudioModName = name;
            _settings.Save();
            ShowBankDetails(_selectedBank);
        };
        AudioEnableOnBuild.IsCheckedChanged += (_, _) =>
        {
            _settings.AudioEnableOnBuild = AudioEnableOnBuild.IsChecked == true;
            _settings.Save();
        };
        AudioSaveXma.IsCheckedChanged += (_, _) =>
        {
            if (!AudioSaveXma.IsEnabled) return;
            _settings.AudioSaveAsXma = AudioSaveXma.IsChecked == true;
            _settings.Save();
        };
        ShowXmaEncoderState();
        BuildBankButton.Click += OnBuildBank;
        StopAudioButton.Click += (_, _) => AudioTools.Stop();
        ExportBankButton.Click += OnExportBank;
        WaveList.AddHandler(Button.ClickEvent, OnWaveButton);
        Closing += (_, _) => AudioTools.Stop();
    }

    private void ShowSoundBanks(SoundBankCatalog catalog, IReadOnlyList<BankRow> banks)
    {
        _soundCatalog = catalog;
        _banks = banks;
        SetFilterChoices(BankFolderBox, AllFolders, banks.Select(b => b.Entry.Folder), _settings.AudioFolder);
    }

    private void FilterBanks(Func<string[], bool> match)
    {
        string? folder = FilterValue(BankFolderBox);
        var rows = _banks
            .Where(b => folder is null || b.Entry.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase))
            .Where(b => match([b.Name, b.Entry.Folder, b.Kind]))
            .ToList();
        ShowRows(BankList, rows, ShowBankDetails);
        BankCount.Text = CountText(rows.Count, _banks.Count);
    }

    /// <summary>
    /// Offers XMA only while the encoder is there, keeping the saved choice for when it comes
    /// back, and says why when it is not.
    /// </summary>
    private void ShowXmaEncoderState()
    {
        bool available = XmaEncoder is not null;
        AudioSaveXma.IsEnabled = available;
        ToolTip.SetTip(AudioSaveXma, available
            ? "Encode replacements to XMA as you save them, so what you play back is what the game will play."
            : "Saving as XMA " + NeedsXmaEncoder);
        bool xma = available && _settings.AudioSaveAsXma;
        AudioSaveXma.IsChecked = xma;
        AudioSaveLossless.IsChecked = !xma;
        ShowBuildState();
    }

    /// <summary>
    /// Build into mod is greyed out while a lossless replacement waits for an encoder that is
    /// not there, since the mod always carries XMA.
    /// </summary>
    private void ShowBuildState()
    {
        bool blocked = _selectedBank is { } bank && XmaEncoder is null && AudioWork.NeedsEncoder(bank.Entry);
        BuildBankButton.IsEnabled = !blocked;
        ToolTip.SetTip(BuildBankButton, blocked
            ? "Building encodes the lossless replacements to XMA, which " + NeedsXmaEncoder
            : "Write this bank with all of its replacements into the mod, as XMA. With none left, the mod's copy is removed.");
    }

    private void ShowBankDetails(BankRow? row)
    {
        _selectedBank = row;
        NoBankText.IsVisible = row is null;
        BankDetailsPanel.IsVisible = row is not null;
        _gameBank = null;
        if (row is null || _soundCatalog is not { } catalog) return;
        ShowBuildState();

        BankTitle.Text = row.Name;
        BankInfo.Text = $"{row.Entry.VfsPath} · {row.Entry.Size / 1024.0 / 1024.0:0.0} MB";
        WavesHeader.Text = "Loading waves…";
        WaveList.ItemsSource = null;
        var work = AudioWork;
        string modName = _settings.AudioModName;
        string? modsRoot = _modCatalog?.ModsRoot;

        Task.Run(() =>
        {
            try
            {
                string path = catalog.FullPath(row.Entry);
                var game = WaveBank.ReadFile(path);
                var replacements = work.Replacements(row.Entry);
                var rows = SoundBankCatalog.Waves(game, path)
                    .Select(w => new WaveRow(w, replacements.GetValueOrDefault(w.Index)))
                    .ToList();
                bool built = modsRoot is not null && work.IsBuilt(row.Entry, modsRoot, modName);
                PostBank(row, () =>
                {
                    _gameBank = game;
                    WaveList.ItemsSource = rows;
                    int replaced = rows.Count(r => r.IsReplaced);
                    WavesHeader.Text = $"Waves ({rows.Count})" + (replaced == 0 ? ""
                        : $", {replaced} replaced" + (built ? $", built into {modName}" : ", not built into the mod yet"));
                    int xma = game.Entries.Count(e => e.Format.Tag == WaveFormatTag.XMA);
                    if (xma > 0 && Ffmpeg is null)
                        WavesHeader.Text += ". XMA waves need FFmpeg (see Settings) to play or export.";
                });
            }
            catch (Exception ex)
            {
                PostBank(row, () => WavesHeader.Text = "Could not read the bank: " + ex.Message);
            }
        });
    }

    private void PostBank(BankRow row, Action update) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(_selectedBank, row)) update();
        });

    private void OnWaveButton(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button { DataContext: WaveRow wave } button || _selectedBank is not { } bank) return;
        if (button.Classes.Contains("wavePlay")) PlayWave(bank, wave);
        else if (button.Classes.Contains("waveExport")) ExportWaves(bank, [wave]);
        else if (button.Classes.Contains("waveReplace")) ReplaceWave(bank, wave);
        else if (button.Classes.Contains("waveRevert")) RevertWave(bank, wave);
    }

    /// <summary>
    /// The entry to play or export: the saved replacement when there is one, else the game's.
    /// A replacement is read from disk, so this runs off the UI thread.
    /// </summary>
    private static WaveBankEntry? EffectiveEntry(WaveBank? game, WaveRow wave) =>
        wave.Replacement is { } replacement ? AudioWorkspace.LoadEntry(replacement) : game?.Entries[wave.Wave.Index];

    private void PlayWave(BankRow bank, WaveRow wave)
    {
        var game = _gameBank;
        string? ffmpeg = Ffmpeg;
        string cache = Path.Combine(Path.GetTempPath(), "ShadowForge", "audio-play.wav");
        SetStatus($"Decoding {bank.Name} {wave.Title}…");
        Task.Run(() =>
        {
            try
            {
                if (EffectiveEntry(game, wave) is not { } entry) return;
                AudioTools.Stop();
                AudioTools.DecodeTo(entry, cache, ffmpeg);
                AudioTools.Play(cache);
                string which = wave.Replacement is { } r ? $" (replacement, {(r.IsXma ? "XMA" : "lossless")})" : $" ({wave.Detail})";
                Dispatcher.UIThread.Post(() => SetStatus($"Playing {bank.Name} {wave.Title}{which}."));
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() => SetStatus($"Could not play {wave.Title}: {ex.Message}"));
            }
        });
    }

    private void OnExportBank(object? sender, RoutedEventArgs e)
    {
        if (_selectedBank is { } bank && WaveList.ItemsSource is IEnumerable<WaveRow> waves)
            ExportWaves(bank, waves.ToList());
    }

    private void ExportWaves(BankRow bank, IReadOnlyList<WaveRow> waves)
    {
        var game = _gameBank;
        string? ffmpeg = Ffmpeg;
        string dir = Path.Combine(_settings.AudioExportRoot, bank.Name);
        ExportBankButton.IsEnabled = false;
        Task.Run(() =>
        {
            int done = 0;
            var failed = new List<string>();
            foreach (var wave in waves)
            {
                string cue = wave.Wave.Cues.FirstOrDefault() is { } c ? "_" + SafeFileName(c) : "";
                string path = Path.Combine(dir, $"{bank.Name}_{wave.Wave.Index:D3}{cue}.wav");
                try
                {
                    AudioTools.DecodeTo(EffectiveEntry(game, wave)!, path, ffmpeg);
                    done++;
                }
                catch (Exception ex)
                {
                    failed.Add($"{wave.Title}: {ex.Message}");
                }
                if (waves.Count > 1 && done % 5 == 0)
                    Dispatcher.UIThread.Post(() => SetStatus($"Exporting {bank.Name}: {done} of {waves.Count}…"));
            }
            Dispatcher.UIThread.Post(() =>
            {
                ExportBankButton.IsEnabled = true;
                string problems = failed.Count == 0 ? "" : $" {failed.Count} failed, the first: {failed[0]}";
                SetStatus($"Exported {done} wave(s) from {bank.Name} to {dir}.{problems}", done > 0 ? dir : null);
            });
        });
    }

    /// <summary>
    /// Converts the chosen audio to 16-bit PCM with the original wave's channels and rate and
    /// saves it as the wave's replacement, encoded to XMA when that is the chosen format.
    /// </summary>
    private async void ReplaceWave(BankRow bank, WaveRow wave)
    {
        if (_gameBank is not { } game) return;
        bool asXma = AudioSaveXma.IsChecked == true;
        string? encoder = XmaEncoder;
        if (asXma && encoder is null)
        {
            ShowXmaEncoderState();
            SetStatus("Saving as XMA " + NeedsXmaEncoder);
            return;
        }

        var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Choose the audio to replace {bank.Name} {wave.Title} with",
            FileTypeFilter =
            [
                new FilePickerFileType("Audio") { Patterns = ["*.wav", "*.mp3", "*.ogg", "*.flac", "*.m4a", "*.opus", "*.aac"] },
            ],
        });
        if (picked is not [var file, ..] || file.TryGetLocalPath() is not { } source) return;

        var original = game.Entries[wave.Wave.Index];
        string? ffmpeg = Ffmpeg;
        var work = AudioWork;
        SetStatus($"{(asXma ? "Encoding" : "Saving")} the replacement for {bank.Name} {wave.Title}…");
        try
        {
            var (saved, seconds) = await Task.Run(() =>
            {
                var wav = AudioTools.LoadAsPcm(source, original.Format.Channels, original.Format.SamplesPerSecond, ffmpeg);
                var replacement = work.Save(bank.Entry, wave.Wave.Index, wav, asXma ? encoder : null);
                return (replacement, wav.FrameCount / (double)wav.SampleRate);
            });
            string form = saved.IsXma ? "as XMA" : "lossless";
            SetStatus($"Saved the replacement for {bank.Name} {wave.Title} {form} ({seconds:0.0} s)."
                + " Use Build into mod to put it in the game.", Path.GetDirectoryName(saved.Path));
            ShowBankDetails(bank);
        }
        catch (Exception ex)
        {
            SetStatus($"Could not replace {wave.Title}: {ex.Message}");
        }
    }

    private void RevertWave(BankRow bank, WaveRow wave)
    {
        try
        {
            AudioWork.Revert(bank.Entry, wave.Wave.Index);
            SetStatus($"Reverted {bank.Name} {wave.Title}. Build into mod again to take it out of the mod.");
            ShowBankDetails(bank);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus($"Could not revert {wave.Title}: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes the game's bank with every saved replacement into the mod, encoding the lossless
    /// ones to XMA.
    /// </summary>
    private async void OnBuildBank(object? sender, RoutedEventArgs e)
    {
        if (_selectedBank is not { } bank || _soundCatalog is not { } sounds) return;
        if (_modCatalog is not { } catalog)
        {
            SetStatus(NeedsModsFolder);
            return;
        }
        string modName = AudioModName.Text?.Trim() ?? "";
        if (!IsValidModName(modName))
        {
            SetStatus("Enter a mod name to build into: letters, digits, spaces, '-' or '_'.");
            return;
        }
        var work = AudioWork;
        string? encoder = XmaEncoder;
        if (encoder is null && work.NeedsEncoder(bank.Entry))
        {
            ShowBuildState();
            SetStatus("Building lossless replacements into a mod encodes them to XMA, which " + NeedsXmaEncoder);
            return;
        }

        _settings.AudioModName = modName;
        _settings.Save();
        bool enable = AudioEnableOnBuild.IsChecked == true;
        string gamePath = sounds.FullPath(bank.Entry);
        BuildBankButton.IsEnabled = false;
        SetStatus($"Building {bank.Name} into mod {modName}…");
        try
        {
            var result = await Task.Run(() => work.Build(bank.Entry, gamePath, catalog.ModsRoot, modName, encoder));
            if (result.Replaced == 0)
            {
                RefreshMods(modName);
                SetStatus($"{bank.Name} has no replacements, so mod {modName} no longer carries it.");
                return;
            }

            string enabled = EnableIfAsked(catalog, modName, enable) ? " and enabled it" : "";
            RefreshMods(modName);
            string encoded = result.Encoded > 0 ? $", encoding {result.Encoded} to XMA" : "";
            SetStatus($"Built {bank.Name} with {result.Replaced} replacement(s) into mod {modName}{enabled}{encoded};"
                + $" the bank went from {result.SizeBefore / 1024.0 / 1024.0:0.0} to {result.SizeAfter / 1024.0 / 1024.0:0.0} MB.",
                Path.GetDirectoryName(result.BankPath));
        }
        catch (Exception ex)
        {
            SetStatus($"Could not build {bank.Name}: {ex.Message}");
        }
        finally
        {
            ShowBankDetails(bank);
        }
    }

    private static string SafeFileName(string name) =>
        new(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
}
