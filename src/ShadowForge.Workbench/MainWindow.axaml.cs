using System.Diagnostics;
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ShadowForge.Formats.HDB;
using ShadowForge.Formats.MAP;
using ShadowForge.Formats.MDL;
using ShadowForge.GameData;
using ShadowForge.GameData.Entities;
using ShadowForge.GameData.Maps;
using ShadowForge.Minimap;

namespace ShadowForge.Workbench;

public sealed record CharacterRow(string Id, string DisplayName, string Category, string ModelDef);

public sealed record MapRow(string Id, string Name, string Region, string Category, string Detail, bool RegionAvailable);

public sealed record StageEntryRow(string Kind, string Name, string Path);

public sealed record ShadingOption(string Label, PreviewShading Value)
{
    public override string ToString() => Label;
}

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
    private ModCatalog? _modCatalog;
    private IReadOnlyList<ModRow> _mods = [];
    private ModRow? _selectedMod;
    private string? _statusFolder;
    private PreviewMesh? _characterTexturesFor;
    private PreviewMesh? _mapTexturesFor;

    private static readonly ShadingOption[] ShadingOptions =
    [
        new("Flat shaded", PreviewShading.Flat),
        new("Smooth shaded", PreviewShading.Smooth),
        new("Wireframe", PreviewShading.Wireframe),
        new("Material colours", PreviewShading.MaterialColors),
        new("Textured", PreviewShading.Textured),
        new("Textured, unlit", PreviewShading.TexturedUnlit),
    ];

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
        var initial = ShadingOptions.FirstOrDefault(o => o.Value == _settings.PreviewShading) ?? ShadingOptions[0];
        foreach (var box in new[] { ShadingBox, MapShadingBox })
        {
            box.ItemsSource = ShadingOptions;
            box.SelectedItem = initial;
            box.SelectionChanged += (_, _) => SetShading((box.SelectedItem as ShadingOption)?.Value);
        }
        Preview.Shading = MapPreview.Shading = initial.Value;
        TextureStatus.IsVisible = MapTextureStatus.IsVisible = initial.Value.NeedsTextures();
        MapList.SelectionChanged += (_, _) => ShowMapDetails(MapList.SelectedItem as MapRow);
        OpenMapInBlenderButton.Click += OnOpenMapInBlender;
        ExportMapButton.Click += OnExportMap;
        ModList.SelectionChanged += (_, _) => ShowModDetails(ModList.SelectedItem as ModRow);
        ModList.AddHandler(Button.ClickEvent, OnModCheckBoxClick);
        AddModButton.Click += OnAddMod;
        OpenModsFolderButton.Click += (_, _) =>
        {
            if (_modCatalog is { } catalog) Process.Start("explorer.exe", catalog.ModsRoot);
        };
        OpenModFolderButton.Click += (_, _) =>
        {
            if (_modCatalog is { } catalog && _selectedMod is { FolderExists: true } row)
                Process.Start("explorer.exe", Path.Combine(catalog.ModsRoot, row.Name));
        };
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
                    .Select(c => new CharacterRow(c.Id, c.DisplayName, c.Class, c.ModelDefRelPath))
                    .ToList();
                var mapResult = new MapCatalog(install).List();
                var maps = mapResult.Stages
                    .Select(m => new MapRow(m.StageId, m.DisplayName, m.RegionIPK, m.Category,
                        $"{m.RegionIPK} · " + (m.RegionAvailable ? $"{m.ModelCount} models" : "region pack missing"),
                        m.RegionAvailable))
                    .ToList();
                var modCatalog = install.ModsRoot is null ? null : new ModCatalog(install);
                var mods = modCatalog?.List() ?? [];

                Dispatcher.UIThread.Post(() =>
                {
                    _install = install;
                    RootText.Text = $"{install.GameDataRoot}  ({install.Source})";
                    _characters = characters;
                    _maps = maps;
                    _mods = mods;
                    ShowModCatalog(modCatalog);
                    ApplyFilter();
                    string modCount = modCatalog is null ? "no mods folder" : $"{mods.Count} mods";
                    SetStatus($"{characters.Count} characters, {maps.Count} stages, {modCount}"
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
        MapList.ItemsSource = _maps.Where(m => Match(m.Id, m.Name, m.Region, m.Category)).ToList();
        FilterMods(null);
    }

    /// <summary>
    /// Refilters only the mod list, so a toggle leaves the other tabs' selections alone, and
    /// reselects <paramref name="select"/> when it is still listed.
    /// </summary>
    private void FilterMods(string? select)
    {
        string q = SearchBox.Text?.Trim() ?? "";
        var rows = _mods.Where(m => q.Length == 0 || m.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        ModList.ItemsSource = rows;
        if (select is not null)
            ModList.SelectedItem = rows.FirstOrDefault(r => r.Name.Equals(select, StringComparison.OrdinalIgnoreCase));
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
        TextureStatus.Text = "";

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
                DetailRig.Text = row.DisplayName == row.Id ? rig : $"{row.DisplayName} · {rig}";
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
                    EnsureCharacterTextures();
                });
            }
            catch (Exception ex)
            {
                Post(row, () => PreviewText.Text = "Preview failed: " + ex.Message);
            }
        });
    }

    /// <summary>
    /// Both previews share one shading, so switching on either tab switches the other.
    /// </summary>
    private void SetShading(PreviewShading? shading)
    {
        if (shading is not { } value || value == Preview.Shading) return;
        Preview.Shading = MapPreview.Shading = value;
        TextureStatus.IsVisible = MapTextureStatus.IsVisible = value.NeedsTextures();
        var option = ShadingOptions.First(o => o.Value == value);
        ShadingBox.SelectedItem = option;
        MapShadingBox.SelectedItem = option;
        _settings.PreviewShading = value;
        _settings.Save();
        EnsureCharacterTextures();
        EnsureMapTextures();
    }

    /// <summary>
    /// Loads the shown character's textures when a textured mode is on. The rig is extracted
    /// to a temp folder for its textures and its texture-override CSV, which renames slots,
    /// so the mesh is rebuilt from the rig's own .hdb before the textures are read.
    /// </summary>
    private void EnsureCharacterTextures()
    {
        if (!Preview.Shading.NeedsTextures() || _selected is not { } row || _install is not { } install
            || Preview.Mesh is not { HasTextures: false } shown || ReferenceEquals(_characterTexturesFor, shown))
            return;

        _characterTexturesFor = shown;
        TextureStatus.Text = "Loading textures…";
        Task.Run(() =>
        {
            string dir = Path.Combine(Path.GetTempPath(), "ShadowForge", "preview-" + Guid.NewGuid().ToString("N"));
            try
            {
                var rig = new EntityAssets(install).MaterializeRig(row.Id, dir);
                if (rig.Skeleton is null) throw new FileNotFoundException("The rig has no model file.");
                var names = rig.TextureOverrideCsv is { } csv ? TexCsvFile.ReadFile(csv).DDSNames : null;
                var model = ModelCooker.Bake(ModelReader.Read(File.ReadAllBytes(rig.Skeleton)));
                var mesh = new PreviewMeshBuilder().Add(model, Matrix4x4.Identity, names).Build().WithTextures(dir);
                Post(row, () =>
                {
                    if (!ReferenceEquals(Preview.Mesh, shown)) return;
                    Preview.ReplaceMesh(mesh);
                    TextureStatus.Text = TextureSummary(mesh);
                });
            }
            catch (Exception ex)
            {
                Post(row, () => TextureStatus.Text = "Textures failed: " + ex.Message);
            }
            finally
            {
                DeleteQuietly(dir);
            }
        });
    }

    /// <summary>
    /// Loads the shown stage's textures when a textured mode is on, from its region pack
    /// extracted to a temp folder.
    /// </summary>
    private void EnsureMapTextures()
    {
        if (!MapPreview.Shading.NeedsTextures() || _selectedMap is not { } row || _install is not { } install
            || MapPreview.Mesh is not { HasTextures: false } shown || ReferenceEquals(_mapTexturesFor, shown))
            return;

        _mapTexturesFor = shown;
        MapTextureStatus.Text = "Loading textures…";
        Task.Run(() =>
        {
            string dir = Path.Combine(Path.GetTempPath(), "ShadowForge", "preview-" + Guid.NewGuid().ToString("N"));
            try
            {
                new MapRegionReader(install, row.Region).ExtractAll(dir);
                var mesh = shown.WithTextures(dir);
                Post(row, () =>
                {
                    if (!ReferenceEquals(MapPreview.Mesh, shown)) return;
                    MapPreview.ReplaceMesh(mesh);
                    MapTextureStatus.Text = TextureSummary(mesh);
                });
            }
            catch (Exception ex)
            {
                Post(row, () => MapTextureStatus.Text = "Textures failed: " + ex.Message);
            }
            finally
            {
                DeleteQuietly(dir);
            }
        });
    }

    private static string TextureSummary(PreviewMesh mesh)
    {
        int total = mesh.MaterialNames.Count;
        return mesh.TexturesFound == total
            ? $"{total} textures"
            : $"{mesh.TexturesFound} of {total} textures found, the rest drawn grey";
    }

    private static void DeleteQuietly(string dir)
    {
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
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
        MapDetailInfo.Text = (row.Name == row.Id ? "" : row.Name + " · ") + $"{row.Category} · region {row.Region}"
            + (row.RegionAvailable ? "" : " (missing)");
        OpenMapInBlenderButton.IsEnabled = row.RegionAvailable;
        ExportMapButton.IsEnabled = row.RegionAvailable;
        StageEntryList.ItemsSource = null;
        MapPreview.Mesh = null;
        MapPreviewHint.IsVisible = false;
        MapTextureStatus.Text = "";
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
                var builder = new PreviewMeshBuilder();
                foreach (var placed in MapAssembler.PlaceModels(files, row.Id))
                    builder.Add(placed.Model, placed.Transform);
                var mesh = builder.Build();
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
                    EnsureMapTextures();
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

    private void Post(ModRow row, Action update) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(_selectedMod, row)) update();
        });

    /// <summary>
    /// A loose extract has no mods folder, so the tab explains that instead of showing an
    /// empty list.
    /// </summary>
    private void ShowModCatalog(ModCatalog? catalog)
    {
        _modCatalog = catalog;
        NoModsText.IsVisible = catalog is null;
        ModsContent.IsVisible = catalog is not null;
        if (catalog is null) return;

        ModOrderText.Text = "Load order: " + catalog.OrderPath;
        ModOrderWarning.Text = catalog.OtherOrderPath is { } other
            ? $"Another mod_order.txt exists at {other}. Only the one above is changed."
            : "";
        ModOrderWarning.IsVisible = catalog.OtherOrderPath is not null;
    }

    private void RefreshMods(string? select)
    {
        if (_modCatalog is not { } catalog) return;
        try
        {
            _mods = catalog.List();
        }
        catch (Exception ex)
        {
            SetStatus("Could not list mods: " + ex.Message);
            return;
        }
        FilterMods(select);
    }

    /// <summary>
    /// A row's checkbox writes mod_order.txt straight away. The list is then reread, since
    /// enabling moves a mod to the end of the load order.
    /// </summary>
    private void OnModCheckBoxClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not CheckBox { DataContext: ModRow row } box || _modCatalog is not { } catalog) return;

        bool enable = box.IsChecked == true;
        try
        {
            catalog.SetEnabled(row.Name, enable);
            SetStatus(enable ? $"Enabled {row.Name}, last in the load order" : $"Disabled {row.Name}");
        }
        catch (Exception ex)
        {
            SetStatus("Could not update mod_order.txt: " + ex.Message);
        }
        RefreshMods(_selectedMod?.Name);
    }

    private void ShowModDetails(ModRow? row)
    {
        _selectedMod = row;
        NoModSelectionText.IsVisible = row is null;
        ModDetailsPanel.IsVisible = row is not null;
        if (row is null || _modCatalog is not { } catalog) return;

        ModDetailTitle.Text = row.Name;
        ModDetailByline.IsVisible = false;
        ModDetailOrder.Text = row.Detail;
        ModDetailDescription.Text = "";
        ModDetailDescription.IsVisible = false;
        OpenModFolderButton.IsEnabled = row.FolderExists;
        ModFilesHeader.Text = "Files";
        ModFileList.ItemsSource = null;

        Task.Run(() =>
        {
            const int maxFiles = 500;
            ModDetails details;
            try
            {
                details = catalog.Details(row.Name, maxFiles);
            }
            catch (Exception ex)
            {
                Post(row, () => ModFilesHeader.Text = "Could not read the mod: " + ex.Message);
                return;
            }
            Post(row, () =>
            {
                ModDetailTitle.Text = details.Title;
                ModDetailByline.Text = details.Byline;
                ModDetailByline.IsVisible = details.Byline is not null;
                ModDetailDescription.Text = details.Description;
                ModDetailDescription.IsVisible = details.Description is not null;
                ModFilesHeader.Text = $"Files ({details.TotalFiles})"
                    + (details.TotalFiles > details.Files.Count ? $", first {details.Files.Count} shown" : "")
                    + (details.Traced || details.TotalFiles == 0 ? "" : " · no access log to check them against");
                ModFileList.ItemsSource = details.Files;
            });
        });
    }

    private async void OnAddMod(object? sender, RoutedEventArgs e)
    {
        if (_modCatalog is not { } catalog) return;

        var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose a mod folder to add",
        });
        if (picked is not [var folder, ..] || folder.TryGetLocalPath() is not { } source) return;

        SetStatus($"Adding {Path.GetFileName(source.TrimEnd('\\', '/'))}…");
        try
        {
            string name = await Task.Run(() => catalog.Install(source));
            RefreshMods(name);
            SetStatus($"Added {name}. It is disabled until you tick it.", Path.Combine(catalog.ModsRoot, name));
        }
        catch (Exception ex)
        {
            SetStatus("Could not add the mod: " + ex.Message);
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
