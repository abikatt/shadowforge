using System.Diagnostics;
using Avalonia.Platform.Storage;

namespace ShadowForge.Workbench;

public sealed record LanguageOption(string Label, string Code)
{
    public override string ToString() => Label;
}

public sealed record TextureQualityOption(string Label, int MaxSize)
{
    public override string ToString() => Label;
}

/// <summary>
/// The Settings tab. Every change is saved as soon as it is made.
/// </summary>
public partial class MainWindow
{
    private static readonly LanguageOption[] Languages =
    [
        new("English", "us"),
        new("Deutsch", "de"),
        new("Español", "es"),
    ];

    private static readonly TextureQualityOption[] TextureQualities =
    [
        new("Low (256 px)", 256),
        new("Medium (512 px)", 512),
        new("High (1024 px)", 1024),
        new("Full size", 0),
    ];

    private bool _updatingSettings;

    /// <summary>
    /// The longest side preview textures are scaled to, from the Texture quality setting.
    /// </summary>
    private int PreviewTextureLimit => _settings.PreviewTextureSize > 0 ? _settings.PreviewTextureSize : int.MaxValue;

    private void SetUpSettings()
    {
        _updatingSettings = true;
        SettingsLanguageBox.ItemsSource = Languages;
        SettingsTextureQualityBox.ItemsSource = TextureQualities;
        _updatingSettings = false;
        ShowSettings();

        SettingsChooseRootButton.Click += OnChooseRoot;
        SettingsDetectRootButton.Click += (_, _) => Load(null, remember: true);
        SettingsLanguageBox.SelectionChanged += (_, _) =>
        {
            if (_updatingSettings || SettingsLanguageBox.SelectedItem is not LanguageOption language
                || language.Code == _settings.NameLanguage) return;
            _settings.NameLanguage = language.Code;
            _settings.Save();
            if (_install is { } install) Load(install.GameDataRoot, remember: false);
        };

        SettingsBrowseBlenderButton.Click += async (_, _) =>
        {
            var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Locate blender.exe",
                FileTypeFilter = [new FilePickerFileType("Blender") { Patterns = ["blender.exe"] }],
            });
            if (picked is [var file, ..] && file.TryGetLocalPath() is { } path) SaveBlenderPath(path);
        };
        SettingsDetectBlenderButton.Click += (_, _) =>
        {
            if (BlenderLauncher.Find(null) is { } found) SaveBlenderPath(found);
            else SetStatus("No Blender found. Use Browse… to pick blender.exe.");
        };

        SettingsBrowseDerivedButton.Click += async (_, _) =>
        {
            if (await PickFolder("Choose where derived characters go") is { } path)
            {
                _settings.DerivedRoot = path;
                SaveSettings();
            }
        };
        SettingsOpenDerivedButton.Click += (_, _) => OpenFolder(_settings.DerivedRoot);
        SettingsBrowseMapExportButton.Click += async (_, _) =>
        {
            if (await PickFolder("Choose where map exports go") is { } path)
            {
                _settings.MapExportRoot = path;
                SaveSettings();
            }
        };
        SettingsOpenMapExportButton.Click += (_, _) => OpenFolder(_settings.MapExportRoot);

        SettingsExportTextures.IsCheckedChanged += (_, _) =>
        {
            if (_updatingSettings) return;
            _settings.ExportTextures = SettingsExportTextures.IsChecked == true;
            _settings.Save();
        };
        SettingsTextureQualityBox.SelectionChanged += (_, _) =>
        {
            if (_updatingSettings || SettingsTextureQualityBox.SelectedItem is not TextureQualityOption quality
                || quality.MaxSize == _settings.PreviewTextureSize) return;
            _settings.PreviewTextureSize = quality.MaxSize;
            _settings.Save();
            ReloadPreviewTextures();
        };
    }

    /// <summary>
    /// Shows the saved settings. The game folder shows what is open when it was detected.
    /// </summary>
    private void ShowSettings()
    {
        _updatingSettings = true;
        SettingsGameRoot.Text = _settings.GameRoot
            ?? (_install is { } install ? $"Detected: {install.GameDataRoot}" : "Detected automatically");
        SettingsLanguageBox.SelectedItem = Languages.FirstOrDefault(l => l.Code == _settings.NameLanguage) ?? Languages[0];
        SettingsBlenderPath.Text = _settings.BlenderPath ?? "Not set. Found on first use, or pick one here.";
        SettingsDerivedRoot.Text = _settings.DerivedRoot;
        SettingsMapExportRoot.Text = _settings.MapExportRoot;
        SettingsExportTextures.IsChecked = _settings.ExportTextures;
        SettingsTextureQualityBox.SelectedItem =
            TextureQualities.FirstOrDefault(q => q.MaxSize == _settings.PreviewTextureSize) ?? TextureQualities[1];
        _updatingSettings = false;
    }

    private void SaveSettings()
    {
        _settings.Save();
        ShowSettings();
    }

    private void SaveBlenderPath(string path)
    {
        _settings.BlenderPath = path;
        SaveSettings();
        SetStatus("Blender: " + path);
    }

    private async Task<string?> PickFolder(string title)
    {
        var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title });
        return picked is [var folder, ..] ? folder.TryGetLocalPath() : null;
    }

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start("explorer.exe", path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus($"Could not open {path}: {ex.Message}");
        }
    }

    /// <summary>
    /// Reselects the shown character and stage so their textures load again at the new size.
    /// Only previews that already have textures are reloaded.
    /// </summary>
    private void ReloadPreviewTextures()
    {
        if (Preview.Mesh is { HasTextures: true }) ShowDetails(_selected);
        if (MapPreview.Mesh is { HasTextures: true }) ShowMapDetails(_selectedMap);
    }
}
