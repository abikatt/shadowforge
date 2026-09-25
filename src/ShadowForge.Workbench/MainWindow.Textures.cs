using Avalonia.Interactivity;
using ShadowForge.GameData.Mods;

namespace ShadowForge.Workbench;

/// <summary>
/// Texture editing on the Characters tab: export a character's textures to PNGs, edit them in
/// any image editor, and deploy the changed ones as a mod.
/// </summary>
public partial class MainWindow
{
    private void SetUpTextures()
    {
        ExportTexturesButton.Click += OnExportTextures;
        OpenTexturesButton.Click += (_, _) =>
        {
            if (_selected is { } row) OpenFolder(Path.Combine(_settings.TextureWorkRoot, row.Id));
        };
        DeployTexturesButton.Click += OnDeployTextures;
    }

    private async void OnExportTextures(object? sender, RoutedEventArgs e)
    {
        if (_selected is not { } row || _install is not { } install) return;

        var workspace = new TextureWorkspace(install, _settings.TextureWorkRoot);
        ExportTexturesButton.IsEnabled = false;
        SetStatus($"Exporting {row.Id}'s textures…");
        try
        {
            var result = await Task.Run(() => workspace.Export(row.Id));
            string kept = result.Kept > 0 ? $", kept {result.Kept} already there" : "";
            SetStatus($"Exported {result.Written} texture(s) for {row.Id} to {result.Dir}{kept}. "
                + "Edit the PNGs, keeping their size, then use Deploy textures as mod.", result.Dir);
        }
        catch (Exception ex)
        {
            SetStatus($"Texture export failed: {ex.Message}");
        }
        finally
        {
            ExportTexturesButton.IsEnabled = true;
        }
    }

    private async void OnDeployTextures(object? sender, RoutedEventArgs e)
    {
        if (_selected is not { } row || _install is not { } install) return;
        if (_modCatalog is not { } catalog)
        {
            SetStatus(NeedsModsFolder);
            return;
        }

        var workspace = new TextureWorkspace(install, _settings.TextureWorkRoot);
        string dir = workspace.DirFor(row.Id);
        if (!Directory.Exists(dir))
        {
            SetStatus($"There are no exported textures for {row.Id}. Use Export textures first.");
            return;
        }

        var installed = catalog.List().Where(m => m.FolderExists).Select(m => m.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var request = await DeployDialog.ForTextures(row.Id, dir, _settings.LastTextureMod ?? row.Id + "_textures", installed)
            .ShowDialog<DeployRequest?>(this);
        if (request is null) return;

        _settings.LastTextureMod = request.ModName;
        _settings.Save();
        DeployTexturesButton.IsEnabled = false;
        SetStatus($"Converting {row.Id}'s edited textures…");
        try
        {
            var result = await Task.Run(() => workspace.Deploy(row.Id, catalog.ModsRoot, request.ModName,
                new ModMetadata(request.ModName, request.Author, request.Version, request.Description)));

            string problems = result.Problems.Count switch
            {
                0 => "",
                1 => " Skipped " + result.Problems[0],
                _ => $" Skipped {result.Problems.Count}, the first: {result.Problems[0]}",
            };
            if (result.Deployed.Count == 0)
            {
                SetStatus($"Nothing to deploy: none of {row.Id}'s {result.Unchanged} PNG(s) differ from the game's textures."
                    + problems, dir);
                return;
            }

            string enabled = EnableIfAsked(catalog, request.ModName, request.Enable) ? " and enabled it" : "";
            RefreshMods(request.ModName);
            SetStatus($"Deployed {result.Deployed.Count} changed texture(s) for {row.Id} to mods\\{request.ModName}{enabled}"
                + $" ({string.Join(", ", result.Deployed.Take(4))}{(result.Deployed.Count > 4 ? ", …" : "")})."
                + problems, result.ModDir);
        }
        catch (Exception ex)
        {
            SetStatus($"Texture deploy failed: {ex.Message}");
        }
        finally
        {
            DeployTexturesButton.IsEnabled = true;
        }
    }
}
