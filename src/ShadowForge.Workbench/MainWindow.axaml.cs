using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ShadowForge.Formats.HDB;
using ShadowForge.GameData;
using ShadowForge.GameData.Entities;
using ShadowForge.GameData.Maps;
using ShadowForge.GameData.Mods;

namespace ShadowForge.Workbench;

public sealed record CharacterRow(string Id, string DisplayName, string Category, string ModelDef);

public sealed record MapRow(string Id, string Region, string Category, string Detail);

public sealed record ModRow(string Name, bool Enabled);

public sealed record FileRow(string Role, string Path, bool Exists)
{
    public string Note => Exists ? "" : "missing";
    public double Opacity => Exists ? 1.0 : 0.6;
}

public partial class MainWindow : Window
{
    private readonly WorkbenchSettings _settings = WorkbenchSettings.Load();
    private GameInstall? _install;
    private IReadOnlyList<CharacterRow> _characters = [];
    private IReadOnlyList<MapRow> _maps = [];
    private CharacterRow? _selected;
    private string? _statusFolder;

    public MainWindow()
    {
        InitializeComponent();
        ChooseRootButton.Click += OnChooseRoot;
        SearchBox.TextChanged += (_, _) => ApplyFilter();
        CharacterList.SelectionChanged += (_, _) => ShowDetails(CharacterList.SelectedItem as CharacterRow);
        OpenInBlenderButton.Click += OnOpenInBlender;
        DeriveButton.Click += OnDerive;
        ShowFolderButton.Click += (_, _) =>
        {
            if (_statusFolder is not null) Process.Start("explorer.exe", _statusFolder);
        };
        Opened += (_, _) => Load(null);
    }

    private async void OnChooseRoot(object? sender, RoutedEventArgs e)
    {
        var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose the Blue Dragon game-data folder",
        });
        if (picked is [var folder, ..] && folder.TryGetLocalPath() is { } path)
            Load(path);
    }

    private void Load(string? explicitRoot)
    {
        SetStatus("Loading…");
        Task.Run(() =>
        {
            try
            {
                var install = GameInstall.Locate(explicitRoot);
                var characters = new EntityCatalog(install).ListChara()
                    .Select(c => new CharacterRow(c.Id, c.DisplayName, c.Category, c.ModelDefRelPath))
                    .ToList();
                var mapResult = new MapCatalog(install).List();
                var maps = mapResult.Stages
                    .Select(m => new MapRow(m.StageId, m.RegionIPK, m.Category,
                        m.RegionAvailable ? $"{m.ModelCount} models" : "region pack missing"))
                    .ToList();
                var mods = install.ModsRoot is null
                    ? []
                    : new ModDeployer(install).List().Select(m => new ModRow(m.Name, m.Enabled)).ToList();

                Dispatcher.UIThread.Post(() =>
                {
                    _install = install;
                    RootText.Text = $"{install.GameDataRoot}  ({install.Source})";
                    _characters = characters;
                    _maps = maps;
                    ModList.ItemsSource = mods;
                    ApplyFilter();
                    SetStatus($"{characters.Count} characters, {maps.Count} stages, {mods.Count} mods"
                        + (mapResult.Warnings.Count > 0 ? $", {mapResult.Warnings.Count} map warnings" : ""));
                });
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    RootText.Text = "No game folder";
                    SetStatus(ex.Message);
                });
            }
        });
    }

    private void ApplyFilter()
    {
        string q = SearchBox.Text?.Trim() ?? "";
        bool Match(params string[] fields) =>
            q.Length == 0 || fields.Any(f => f.Contains(q, StringComparison.OrdinalIgnoreCase));

        CharacterList.ItemsSource = _characters.Where(c => Match(c.Id, c.DisplayName, c.Category)).ToList();
        MapList.ItemsSource = _maps.Where(m => Match(m.Id, m.Region, m.Category)).ToList();
    }

    /// <summary>
    /// Resolves and renders on a worker thread. A result that arrives after the selection has
    /// moved on is dropped, so fast scrolling never shows the wrong character.
    /// </summary>
    private void ShowDetails(CharacterRow? row)
    {
        _selected = row;
        NoSelectionText.IsVisible = row is null;
        DetailsPanel.IsVisible = row is not null;
        if (row is null || _install is not { } install) return;

        DetailId.Text = row.Id;
        DetailRig.Text = "Resolving…";
        FileList.ItemsSource = null;
        Preview.Mesh = null;
        PreviewHint.IsVisible = false;
        PreviewText.Text = "Loading model…";

        Task.Run(() =>
        {
            ResolvedEntity entity;
            try
            {
                entity = new EntityResolver(install).Resolve(row.Id);
            }
            catch (Exception ex)
            {
                Post(row, () =>
                {
                    DetailRig.Text = "Could not resolve: " + ex.Message;
                    PreviewText.Text = "";
                });
                return;
            }

            var files = entity.Files.Select(f => new FileRow(f.Role.ToString(), f.VfsPath, f.Exists)).ToList();
            string rig = entity.RigId == entity.Id
                ? $"Class {entity.Class}, own rig"
                : $"Class {entity.Class}, shared rig {entity.RigClass}\\{entity.RigId}";
            Post(row, () =>
            {
                DetailRig.Text = rig;
                FileList.ItemsSource = files;
            });

            var skeleton = entity.Files.FirstOrDefault(f => f.Role == FileRole.Skeleton && f.Exists);
            if (skeleton is null)
            {
                Post(row, () => PreviewText.Text = "No model file to preview.");
                return;
            }
            try
            {
                byte[] hdb = new GameFileSystem(install).ReadVfs(skeleton.VfsPath);
                var mesh = PreviewRenderer.Prepare(ModelCooker.Bake(ModelReader.Read(hdb)));
                Post(row, () =>
                {
                    Preview.Mesh = mesh;
                    PreviewHint.IsVisible = true;
                    PreviewText.Text = "";
                });
            }
            catch (Exception ex)
            {
                Post(row, () => PreviewText.Text = "Preview failed: " + ex.Message);
            }
        });
    }

    private void Post(CharacterRow row, Action update) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(_selected, row)) update();
        });

    private async void OnOpenInBlender(object? sender, RoutedEventArgs e)
    {
        if (_selected is not { } row || _install is not { } install) return;

        string? blender = BlenderLauncher.Find(_settings.BlenderPath);
        if (blender is null)
        {
            var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Locate blender.exe",
                FileTypeFilter = [new FilePickerFileType("Blender") { Patterns = ["blender.exe"] }],
            });
            blender = picked is [var file, ..] ? file.TryGetLocalPath() : null;
            if (blender is null) return;
        }
        if (_settings.BlenderPath != blender)
        {
            _settings.BlenderPath = blender;
            _settings.Save();
        }

        try
        {
            BlenderLauncher.OpenEntity(blender, row.Id, install.GameDataRoot);
            SetStatus($"Opening {row.Id} in Blender…");
        }
        catch (Exception ex)
        {
            SetStatus("Could not start Blender: " + ex.Message);
        }
    }

    private async void OnDerive(object? sender, RoutedEventArgs e)
    {
        if (_selected is not { } row || _install is not { } install) return;

        var existing = _characters.Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string? newId = await new DeriveDialog(row.Id, existing, _settings.DerivedRoot).ShowDialog<string?>(this);
        if (newId is null) return;

        string outDir = Path.Combine(_settings.DerivedRoot, newId);
        SetStatus($"Deriving {newId} from {row.Id}…");
        try
        {
            var result = await Task.Run(() =>
                new EntityDeriver(install).Derive(row.Id, newId, EntityId.ClassFor(newId)!, outDir));
            SetStatus($"Derived {result.Id} from {result.SourceId}: {result.Files.Count} files in {outDir}", outDir);
        }
        catch (Exception ex)
        {
            SetStatus($"Derive failed: {ex.Message}");
        }
    }

    private void SetStatus(string text, string? folder = null)
    {
        StatusText.Text = text;
        _statusFolder = folder;
        ShowFolderButton.IsVisible = folder is not null;
    }
}
