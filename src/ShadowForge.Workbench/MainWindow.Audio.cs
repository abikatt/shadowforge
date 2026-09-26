using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ShadowForge.Formats.XACT;
using ShadowForge.GameData.Audio;
using ShadowForge.GameData.Mods;

namespace ShadowForge.Workbench;

public sealed record BankRow(SoundBankEntry Entry)
{
    public string Name => Entry.Name;
    public string Kind => Entry.Kind;
    public string Detail => $"{Entry.Folder} · {Entry.Size / 1024.0 / 1024.0:0.0} MB";
}

public sealed record WaveRow(SoundWave Wave, string Status)
{
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
/// The Audio tab: the game's wave banks, whose waves can be played, exported as .wav, and
/// replaced in a copy of the bank inside a mod. Replacing again builds on the mod's copy, so
/// earlier replacements are kept.
/// </summary>
public partial class MainWindow
{
    private const string AllFolders = "All folders";

    private SoundBankCatalog? _soundCatalog;
    private IReadOnlyList<BankRow> _banks = [];
    private BankRow? _selectedBank;
    private WaveBank? _gameBank;
    private WaveBank? _modBank;

    private string? Ffmpeg => AudioTools.FindFfmpeg(_settings.FfmpegPath);

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

    private string ModBankPath(BankRow row) =>
        Path.Combine(_modCatalog!.ModsRoot, _settings.AudioModName, row.Entry.VfsPath);

    private void ShowBankDetails(BankRow? row)
    {
        _selectedBank = row;
        NoBankText.IsVisible = row is null;
        BankDetailsPanel.IsVisible = row is not null;
        _gameBank = _modBank = null;
        if (row is null || _soundCatalog is not { } catalog) return;

        BankTitle.Text = row.Name;
        BankInfo.Text = $"{row.Entry.VfsPath} · {row.Entry.Size / 1024.0 / 1024.0:0.0} MB";
        WavesHeader.Text = "Loading waves…";
        WaveList.ItemsSource = null;
        string? modPath = _modCatalog is not null ? ModBankPath(row) : null;

        Task.Run(() =>
        {
            try
            {
                string path = catalog.FullPath(row.Entry);
                var game = WaveBank.ReadFile(path);
                var mod = modPath is not null && File.Exists(modPath) ? WaveBank.ReadFile(modPath) : null;
                var rows = SoundBankCatalog.Waves(game, path)
                    .Select(w => new WaveRow(w, mod is not null && IsReplaced(game, mod, w.Index) ? "replaced in mod" : ""))
                    .ToList();
                PostBank(row, () =>
                {
                    _gameBank = game;
                    _modBank = mod;
                    WaveList.ItemsSource = rows;
                    int replaced = rows.Count(r => r.Status.Length > 0);
                    WavesHeader.Text = $"Waves ({rows.Count})" + (replaced > 0 ? $", {replaced} replaced in {_settings.AudioModName}" : "");
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

    private static bool IsReplaced(WaveBank game, WaveBank mod, int index) =>
        index < mod.Entries.Count
        && (mod.Entries[index].Format != game.Entries[index].Format
            || mod.Entries[index].Data.Length != game.Entries[index].Data.Length
            || mod.Entries[index].DurationSamples != game.Entries[index].DurationSamples);

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
    }

    /// <summary>
    /// The entry to play or export: the mod's replacement when there is one, else the game's.
    /// </summary>
    private WaveBankEntry? EffectiveEntry(WaveRow wave) =>
        wave.Status.Length > 0 && _modBank is { } mod ? mod.Entries[wave.Wave.Index]
        : _gameBank?.Entries[wave.Wave.Index];

    private void PlayWave(BankRow bank, WaveRow wave)
    {
        if (EffectiveEntry(wave) is not { } entry) return;
        string? ffmpeg = Ffmpeg;
        string cache = Path.Combine(Path.GetTempPath(), "ShadowForge", "audio-play.wav");
        SetStatus($"Decoding {bank.Name} {wave.Title}…");
        Task.Run(() =>
        {
            try
            {
                AudioTools.Stop();
                AudioTools.DecodeTo(entry, cache, ffmpeg);
                AudioTools.Play(cache);
                Dispatcher.UIThread.Post(() => SetStatus($"Playing {bank.Name} {wave.Title} ({wave.Detail})."));
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
        var entries = waves.Select(w => (Wave: w, Entry: EffectiveEntry(w))).Where(p => p.Entry is not null).ToList();
        string? ffmpeg = Ffmpeg;
        string dir = Path.Combine(_settings.AudioExportRoot, bank.Name);
        ExportBankButton.IsEnabled = false;
        Task.Run(() =>
        {
            int done = 0;
            var failed = new List<string>();
            foreach (var (wave, entry) in entries)
            {
                string cue = wave.Wave.Cues.FirstOrDefault() is { } c ? "_" + SafeFileName(c) : "";
                string path = Path.Combine(dir, $"{bank.Name}_{wave.Wave.Index:D3}{cue}.wav");
                try
                {
                    AudioTools.DecodeTo(entry!, path, ffmpeg);
                    done++;
                }
                catch (Exception ex)
                {
                    failed.Add($"{wave.Title}: {ex.Message}");
                }
                if (entries.Count > 1 && done % 5 == 0)
                    Dispatcher.UIThread.Post(() => SetStatus($"Exporting {bank.Name}: {done} of {entries.Count}…"));
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
    /// Converts the chosen audio to 16-bit PCM with the original wave's channels and rate, and
    /// writes it into the mod's copy of the bank, making that copy from the game's bank first.
    /// </summary>
    private async void ReplaceWave(BankRow bank, WaveRow wave)
    {
        if (_modCatalog is not { } catalog)
        {
            SetStatus(NeedsModsFolder);
            return;
        }
        string modName = AudioModName.Text?.Trim() ?? "";
        if (!IsValidModName(modName))
        {
            SetStatus("Enter a mod name to replace into: letters, digits, spaces, '-' or '_'.");
            return;
        }
        if (_gameBank is not { } game || _soundCatalog is not { } sounds) return;

        var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Choose the audio to replace {bank.Name} {wave.Title} with",
            FileTypeFilter =
            [
                new FilePickerFileType("Audio") { Patterns = ["*.wav", "*.mp3", "*.ogg", "*.flac", "*.m4a", "*.opus", "*.aac"] },
            ],
        });
        if (picked is not [var file, ..] || file.TryGetLocalPath() is not { } source) return;

        _settings.AudioModName = modName;
        _settings.Save();
        var original = game.Entries[wave.Wave.Index];
        string? ffmpeg = Ffmpeg;
        bool enable = AudioEnableOnBuild.IsChecked == true;
        string gamePath = sounds.FullPath(bank.Entry);
        string modPath = ModBankPath(bank);
        SetStatus($"Replacing {bank.Name} {wave.Title}…");
        try
        {
            var (before, after, samples) = await Task.Run(() =>
            {
                var wav = AudioTools.LoadAsPcm(source, original.Format.Channels, original.Format.SamplesPerSecond, ffmpeg);
                var target = WaveBank.ReadFile(File.Exists(modPath) ? modPath : gamePath);
                long sizeBefore = new FileInfo(File.Exists(modPath) ? modPath : gamePath).Length;
                target.ReplaceEntry(wave.Wave.Index, wav);
                byte[] written = target.Write();
                Directory.CreateDirectory(Path.GetDirectoryName(modPath)!);
                File.WriteAllBytes(modPath, written);

                string toml = Path.Combine(catalog.ModsRoot, modName, "mod.toml");
                if (!File.Exists(toml))
                    ModDeployer.WriteToml(toml, new ModMetadata(modName, null, null, "Audio replacements"));
                return (sizeBefore, (long)written.Length, wav.FrameCount);
            });

            string enabled = EnableIfAsked(catalog, modName, enable) ? " and enabled it" : "";
            RefreshMods(modName);
            string codec = original.Format.Tag == WaveFormatTag.PCM
                ? ""
                : $" The wave is now PCM instead of {original.Format.Tag}, which has not been tried in the game yet;";
            SetStatus($"Replaced {bank.Name} {wave.Title} in mod {modName}{enabled}.{codec}"
                + $" the bank went from {before / 1024.0 / 1024.0:0.0} to {after / 1024.0 / 1024.0:0.0} MB"
                + $" ({samples / (double)original.Format.SamplesPerSecond:0.0} s of audio).", Path.GetDirectoryName(modPath));
            ShowBankDetails(bank);
        }
        catch (Exception ex)
        {
            SetStatus($"Could not replace {wave.Title}: {ex.Message}");
        }
    }

    private static string SafeFileName(string name) =>
        new(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
}
