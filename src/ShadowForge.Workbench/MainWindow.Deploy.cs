using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowForge.GameData.Entities;
using ShadowForge.GameData.Mods;

namespace ShadowForge.Workbench;

/// <summary>
/// Making mods from edits: Deploy edit as mod on the Characters tab (the CLI's entity deploy)
/// and Build mod from source on the Mods tab (mod build). Both need a mods folder.
/// </summary>
public partial class MainWindow
{
    private const string NeedsModsFolder =
        "Making mods needs a reblue install with a mods folder. Choose one with Game folder….";

    private void SetUpDeploy()
    {
        DeployEditButton.Click += OnDeployEdit;
        BuildModButton.Click += OnBuildModSource;
    }

    /// <summary>
    /// Cooks the edited model into a temp folder, maps each cooked file to the path it
    /// overrides, and writes them into the mod.
    /// </summary>
    private async void OnDeployEdit(object? sender, RoutedEventArgs e)
    {
        if (_selected is not { } row || _install is not { } install) return;
        if (_modCatalog is not { } catalog)
        {
            SetStatus(NeedsModsFolder);
            return;
        }

        string workDir = Path.Combine(Path.GetTempPath(), "sforge_work", row.Id);
        string? suggested = new[] { ".glb", ".gltf" }
            .Select(ext => Path.Combine(workDir, row.Id + ext))
            .FirstOrDefault(File.Exists);
        var installed = catalog.List().Where(m => m.FolderExists).Select(m => m.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var request = await new DeployDialog(row.Id, suggested, _settings.LastDeployMod ?? row.Id + "_edit", installed)
            .ShowDialog<DeployRequest?>(this);
        if (request is null) return;

        _settings.LastDeployMod = request.ModName;
        _settings.Save();
        DeployEditButton.IsEnabled = false;
        SetStatus($"Cooking {row.Id} from {Path.GetFileName(request.ModelPath)}…");
        try
        {
            var (deploy, warnings) = await Task.Run(() =>
            {
                string cooked = Path.Combine(Path.GetTempPath(), "ShadowForge", "cook-" + Guid.NewGuid().ToString("N"));
                try
                {
                    var entity = new EntityResolver(install).Resolve(row.Id);
                    var cook = new EntityCooker(install).Cook(entity, request.ModelPath, cooked, NullLogger.Instance);
                    var plan = new EntityPacker(install).BuildPlan(row.Id, cooked);
                    var result = new ModDeployer(install).Deploy(plan, request.ModName,
                        new ModMetadata(request.ModName, request.Author, request.Version, request.Description));
                    return (result, cook.Warnings.Concat(plan.Skipped.Select(s => "not a game file: " + s)).ToList());
                }
                finally
                {
                    DeleteQuietly(cooked);
                }
            });

            string enabled = EnableIfAsked(catalog, request.ModName, request.Enable) ? " and enabled it" : "";
            RefreshMods(request.ModName);
            string warned = warnings.Count switch
            {
                0 => "",
                1 => " Warning: " + warnings[0],
                _ => $" {warnings.Count} warnings, the first: {warnings[0]}",
            };
            SetStatus($"Deployed {deploy.FilesWritten} file(s) for {row.Id} to mods\\{request.ModName}{enabled}.{warned}",
                deploy.ModDir);
        }
        catch (Exception ex)
        {
            SetStatus($"Deploy failed: {ex.Message}");
        }
        finally
        {
            DeployEditButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Builds a declared mod source into mods\{name}, updating the mod when it is installed.
    /// The mod is left as it was in the load order; a new one starts disabled.
    /// </summary>
    private async void OnBuildModSource(object? sender, RoutedEventArgs e)
    {
        if (_modCatalog is not { } catalog || _install is not { } install)
        {
            SetStatus(NeedsModsFolder);
            return;
        }

        var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose a mod source folder (a mod .toml and entities\\<id>\\entity.toml)",
        });
        if (picked is not [var folder, ..] || folder.TryGetLocalPath() is not { } sourceDir) return;

        SetStatus($"Building {Path.GetFileName(sourceDir.TrimEnd('\\', '/'))}…");
        BuildModButton.IsEnabled = false;
        try
        {
            var (source, result) = await Task.Run(() =>
            {
                var loaded = ModSource.Load(sourceDir);
                return (loaded, new ModBuilder(install).Build(loaded, catalog.ModsRoot));
            });
            string modName = Path.GetFileName(result.ModDir);
            bool enabled = catalog.List().Any(m => m.Enabled && m.Name.Equals(modName, StringComparison.OrdinalIgnoreCase));
            RefreshMods(modName);
            SetStatus($"Built {source.Name}: {result.FilesWritten} file(s) for {string.Join(", ", result.Entities)}."
                + (enabled ? "" : " Tick it to enable it."), result.ModDir);
        }
        catch (Exception ex)
        {
            SetStatus($"Build failed: {ex.Message}");
        }
        finally
        {
            BuildModButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Enables the mod when asked and it is not already enabled, so a rebuild does not move an
    /// enabled mod to the end of the load order. Returns whether it enabled it.
    /// </summary>
    private static bool EnableIfAsked(ModCatalog catalog, string modName, bool enable)
    {
        if (!enable || catalog.List().Any(m => m.Enabled && m.Name.Equals(modName, StringComparison.OrdinalIgnoreCase)))
            return false;
        catalog.SetEnabled(modName, true);
        return true;
    }
}
