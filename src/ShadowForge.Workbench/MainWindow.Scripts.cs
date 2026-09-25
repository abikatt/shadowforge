using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Win32;
using ShadowForge.GameData;
using ShadowForge.GameData.Scripts;

namespace ShadowForge.Workbench;

/// <summary>
/// The Maps tab's Scene scripts section: each script of the selected stage can be decompiled
/// to the scripts folder for editing, then compiled into a mod at the path the game reads.
/// </summary>
public partial class MainWindow
{
    private IReadOnlyDictionary<string, List<SceneScriptEntry>> _scriptsByStage =
        new Dictionary<string, List<SceneScriptEntry>>();

    private SceneScriptWorkspace? Scripts =>
        _install is { } install ? new SceneScriptWorkspace(new GameFileSystem(install), _settings.ScriptWorkRoot) : null;

    private void SetUpScripts()
    {
        ScriptModName.Text = _settings.ScriptModName;
        ScriptEnableOnBuild.IsChecked = _settings.ScriptEnableOnBuild;
        ScriptModName.LostFocus += (_, _) =>
        {
            string name = ScriptModName.Text?.Trim() ?? "";
            if (name == _settings.ScriptModName || !IsValidModName(name)) return;
            _settings.ScriptModName = name;
            _settings.Save();
            ShowScripts(_selectedMap);
        };
        ScriptEnableOnBuild.IsCheckedChanged += (_, _) =>
        {
            _settings.ScriptEnableOnBuild = ScriptEnableOnBuild.IsChecked == true;
            _settings.Save();
        };
        ScriptList.AddHandler(Button.ClickEvent, OnScriptButton);
    }

    /// <summary>
    /// Indexes every scene script by the stage it drives. Runs on the loading thread.
    /// </summary>
    private static Dictionary<string, List<SceneScriptEntry>> IndexScripts(GameInstall install, IEnumerable<string> stageIds)
    {
        var stages = stageIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new SceneScriptCatalog(new GameFileSystem(install)).List(stages)
            .Where(s => s.StageId is not null)
            .GroupBy(s => s.StageId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.FileName, StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    private void ShowScripts(MapRow? row)
    {
        var scripts = row is not null && _scriptsByStage.TryGetValue(row.Id, out var list) ? list : [];
        ScriptsHeader.Text = scripts.Count == 0 ? "Scene scripts: none for this stage" : $"Scene scripts ({scripts.Count})";
        ScriptModRow.IsVisible = scripts.Count > 0;
        ScriptList.ItemsSource = Scripts is { } workspace
            ? scripts.Select(s => new ScriptRow(s, ScriptStatus(workspace, s))).ToList()
            : null;
    }

    private string ScriptStatus(SceneScriptWorkspace workspace, SceneScriptEntry script)
    {
        if (!workspace.IsEdited(script)) return "";
        bool built = _modCatalog is { } catalog
                     && SceneScriptWorkspace.IsInMod(script, catalog.ModsRoot, _settings.ScriptModName);
        return built ? "edited · built" : "edited";
    }

    private void OnScriptButton(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button { DataContext: ScriptRow row } button) return;
        if (button.Classes.Contains("scriptEdit")) EditScript(row);
        else if (button.Classes.Contains("scriptBuild")) BuildScript(row);
    }

    private void EditScript(ScriptRow row)
    {
        if (Scripts is not { } workspace) return;
        var map = _selectedMap;
        Task.Run(() =>
        {
            try
            {
                var opened = workspace.Open(row.Entry);
                Dispatcher.UIThread.Post(() =>
                {
                    OpenInEditor(opened.Path);
                    string warning = opened.RoundTrips
                        ? ""
                        : " Warning: the shipped script does not rebuild identically, so building it may lose data.";
                    SetStatus((opened.Created ? $"Decompiled {row.FileName} to {opened.Path}."
                        : $"Opened your existing edit of {row.FileName}.") + warning, Path.GetDirectoryName(opened.Path));
                    if (ReferenceEquals(_selectedMap, map)) ShowScripts(map);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() => SetStatus($"Could not decompile {row.FileName}: {ex.Message}"));
            }
        });
    }

    /// <summary>
    /// Compiles the edit. With a mods folder it is written into the mod and, if asked, the mod
    /// is enabled; a loose extract has nowhere to deploy, so the compile only checks the edit.
    /// </summary>
    private void BuildScript(ScriptRow row)
    {
        if (Scripts is not { } workspace) return;
        if (!workspace.IsEdited(row.Entry))
        {
            SetStatus($"There is no edit of {row.FileName} to build yet. Use Edit first.");
            return;
        }
        string modName = ScriptModName.Text?.Trim() ?? "";
        if (!IsValidModName(modName))
        {
            SetStatus("Enter a mod name to build into: letters, digits, spaces, '-' or '_'.");
            return;
        }
        var catalog = _modCatalog;
        bool enable = ScriptEnableOnBuild.IsChecked == true;
        var map = _selectedMap;
        SetStatus($"Building {row.FileName}…");
        Task.Run(() =>
        {
            try
            {
                bool roundTrips = workspace.ShippedRoundTrips(row.Entry);
                string warning = roundTrips ? "" : " Warning: this script does not rebuild identically, so some data may be lost.";
                if (catalog is null)
                {
                    workspace.Compile(row.Entry);
                    Dispatcher.UIThread.Post(() => SetStatus(
                        $"{row.FileName} compiles. This game folder has no mods folder, so nothing was deployed." + warning));
                    return;
                }

                string dest = workspace.Deploy(row.Entry, catalog.ModsRoot, modName);
                bool wasEnabled = catalog.List().Any(m => m.Enabled && m.Name.Equals(modName, StringComparison.OrdinalIgnoreCase));
                if (enable && !wasEnabled) catalog.SetEnabled(modName, true);
                Dispatcher.UIThread.Post(() =>
                {
                    _settings.ScriptModName = modName;
                    _settings.Save();
                    RefreshMods(_selectedMod?.Name);
                    string enabled = enable && !wasEnabled ? " and enabled it" : "";
                    SetStatus($"Built {row.FileName} into mod {modName}{enabled}." + warning, Path.GetDirectoryName(dest));
                    if (ReferenceEquals(_selectedMap, map)) ShowScripts(map);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() => SetStatus($"{row.FileName} did not compile: {ex.Message}"));
            }
        });
    }

    private static bool IsValidModName(string name) =>
        name.Length > 0 && name.All(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_');

    /// <summary>
    /// Opens a file in the program Windows associates with its extension, else in Notepad,
    /// so a first edit does not stop at the "How do you want to open this file?" prompt.
    /// </summary>
    private static void OpenInEditor(string path)
    {
        if (HasAssociation(Path.GetExtension(path)))
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                return;
            }
            catch (Win32Exception)
            {
            }
        }
        Process.Start("notepad.exe", path);
    }

    private static bool HasAssociation(string extension)
    {
        if (!OperatingSystem.IsWindows()) return true;
        using var userChoice = Registry.CurrentUser.OpenSubKey(
            $@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\UserChoice");
        if (userChoice?.GetValue("ProgId") is string { Length: > 0 }) return true;
        using var classKey = Registry.ClassesRoot.OpenSubKey(extension);
        return classKey?.GetValue(null) is string { Length: > 0 };
    }
}
