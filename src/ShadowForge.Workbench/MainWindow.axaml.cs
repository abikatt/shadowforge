using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ShadowForge.Formats.HDB;
using ShadowForge.Formats.MAP;
using ShadowForge.GameData;
using ShadowForge.GameData.Entities;
using ShadowForge.GameData.Maps;
using ShadowForge.GameData.Mods;
using ShadowForge.Minimap;

namespace ShadowForge.Workbench;

public sealed record CharacterRow(string Id, string DisplayName, string Category, string ModelDef);

public sealed record MapRow(string Id, string Region, string Category, string Detail, bool RegionAvailable);

public sealed record StageEntryRow(string Kind, string Name, string Path);

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
    private MapRow? _selectedMap;
    private string? _statusFolder;

    /// <summary>
    /// Looks down on a stage from above; a negative pitch puts the camera above the model.
    /// </summary>
    private static readonly PreviewCamera StageCamera = PreviewCamera.Default with { PitchDeg = -45f };

    public MainWindow()
    {
        InitializeComponent();
        ChooseRootButton.Click += OnChooseRoot;
        SearchBox.TextChanged += (_, _) => ApplyFilter();
        CharacterList.SelectionChanged += (_, _) => ShowDetails(CharacterList.SelectedItem as CharacterRow);
        OpenInBlenderButton.Click += OnOpenInBlender;
        DeriveButton.Click += OnDerive;
        MapPreview.HomeCamera = StageCamera;
        MapList.SelectionChanged += (_, _) => ShowMapDetails(MapList.SelectedItem as MapRow);
        OpenMapInBlenderButton.Click += OnOpenMapInBlender;
        ExportMapButton.Click += OnExportMap;
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
                        m.RegionAvailable ? $"{m.ModelCount} models" : "region pack missing", m.RegionAvailable))
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

    private void Post(MapRow row, Action update) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(_selectedMap, row)) update();
        });

    /// <summary>
    /// Lists the stage def's entries at once, then assembles every placed model on a worker
    /// thread for the preview. Sky, water and light rigs are left out by the assembler, so the
    /// view frames the walkable stage rather than the skydome.
    /// </summary>
    private void ShowMapDetails(MapRow? row)
    {
        _selectedMap = row;
        NoMapText.IsVisible = row is null;
        MapDetailsPanel.IsVisible = row is not null;
        if (row is null || _install is not { } install) return;

        MapDetailId.Text = row.Id;
        MapDetailInfo.Text = $"{row.Category} · region {row.Region}"
            + (row.RegionAvailable ? "" : " (missing)");
        OpenMapInBlenderButton.IsEnabled = row.RegionAvailable;
        ExportMapButton.IsEnabled = row.RegionAvailable;
        StageEntryList.ItemsSource = null;
        MapPreview.Mesh = null;
        MapPreviewHint.IsVisible = false;
        MapPreviewText.Text = row.RegionAvailable ? "Assembling stage…" : "Region pack missing, nothing to preview.";

        Task.Run(() =>
        {
            var files = new GameFileSystem(install);
            try
            {
                var map = StageDef.Read(files.ReadMapDef(row.Id));
                var entries = map.Models
                    .Select(m => new StageEntryRow("MODEL", $"{m.Name} · area {m.Area}", m.ObjectHDB ?? "(no OBJECT)"))
                    .Concat(map.Parts.Select(p => new StageEntryRow("PART", p.Kind, p.Path)))
                    .ToList();
                Post(row, () => StageEntryList.ItemsSource = entries);
            }
            catch (Exception ex)
            {
                Post(row, () =>
                {
                    MapDetailInfo.Text = "Could not read stage def: " + ex.Message;
                    MapPreviewText.Text = "";
                });
                return;
            }
            if (!row.RegionAvailable) return;

            try
            {
                var mesh = MapAssembler.AssembleStage(files, row.Id).ToPreviewMesh();
                Post(row, () =>
                {
                    if (mesh.TriangleCount == 0)
                    {
                        MapPreviewText.Text = "No stage geometry to preview.";
                        return;
                    }
                    MapPreview.Mesh = mesh;
                    MapPreviewHint.IsVisible = true;
                    MapPreviewText.Text = "";
                });
            }
            catch (Exception ex)
            {
                Post(row, () => MapPreviewText.Text = "Preview failed: " + ex.Message);
            }
        });
    }

    private async void OnOpenMapInBlender(object? sender, RoutedEventArgs e)
    {
        if (_selectedMap is not { } row || _install is not { } install) return;
        if (await LocateBlenderAsync() is not { } blender) return;

        try
        {
            BlenderLauncher.OpenMap(blender, row.Id, install.GameDataRoot);
            SetStatus($"Opening {row.Id} in Blender…");
        }
        catch (Exception ex)
        {
            SetStatus("Could not start Blender: " + ex.Message);
        }
    }

    private async void OnExportMap(object? sender, RoutedEventArgs e)
    {
        if (_selectedMap is not { } row || _install is not { } install) return;

        string outDir = Path.Combine(_settings.MapExportRoot, row.Id);
        SetStatus($"Exporting {row.Id}…");
        ExportMapButton.IsEnabled = false;
        try
        {
            var result = await Task.Run(() => new MapExporter(install).Export(row.Id, outDir,
                progress: line => Dispatcher.UIThread.Post(() => SetStatus($"Exporting {row.Id}: {line}"))));
            string warnings = result.Warnings.Count > 0 ? $" ({result.Warnings.Count} warnings)" : "";
            SetStatus($"Exported {row.Id} to {result.GlbPath}{warnings}", outDir);
        }
        catch (Exception ex)
        {
            SetStatus($"Export failed: {ex.Message}");
        }
        finally
        {
            ExportMapButton.IsEnabled = _selectedMap?.RegionAvailable ?? false;
        }
    }

    /// <summary>
    /// The saved or discovered blender.exe, else one the user picks. A new choice is saved.
    /// </summary>
    private async Task<string?> LocateBlenderAsync()
    {
        string? blender = BlenderLauncher.Find(_settings.BlenderPath);
        if (blender is null)
        {
            var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Locate blender.exe",
                FileTypeFilter = [new FilePickerFileType("Blender") { Patterns = ["blender.exe"] }],
            });
            blender = picked is [var file, ..] ? file.TryGetLocalPath() : null;
            if (blender is null) return null;
        }
        if (_settings.BlenderPath != blender)
        {
            _settings.BlenderPath = blender;
            _settings.Save();
        }
        return blender;
    }

    private async void OnOpenInBlender(object? sender, RoutedEventArgs e)
    {
        if (_selected is not { } row || _install is not { } install) return;
        if (await LocateBlenderAsync() is not { } blender) return;

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
