using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;

namespace ShadowForge.Workbench;

/// <summary>
/// A program Edit can open scene scripts in. A null path means the program Windows associates with .bdsl.
/// </summary>
public sealed record EditorOption(string Label, string? Path)
{
    public override string ToString() => Label;
}

/// <summary>
/// Finds text editors and opens scene scripts in the one the Script editor setting names.
/// </summary>
public static class ScriptEditors
{
    public static readonly EditorOption WindowsDefault = new("Windows default", null);

    /// <summary>
    /// Windows default, then each known editor that is installed, then Notepad.
    /// </summary>
    public static List<EditorOption> Detect()
    {
        var options = new List<EditorOption> { WindowsDefault };
        if (FindVsCode() is { } code) options.Add(new("Visual Studio Code", code));
        if (FindNotepadPlusPlus() is { } npp) options.Add(new("Notepad++", npp));
        options.Add(new("Notepad", "notepad.exe"));
        return options;
    }

    /// <summary>
    /// The option for a saved path: a detected editor's own entry, else one labelled by file name.
    /// </summary>
    public static EditorOption OptionFor(string? path, IReadOnlyList<EditorOption> detected)
    {
        if (path is null) return WindowsDefault;
        return detected.FirstOrDefault(o => string.Equals(o.Path, path, StringComparison.OrdinalIgnoreCase))
            ?? new EditorOption(Path.GetFileNameWithoutExtension(path), path);
    }

    /// <summary>
    /// Opens the file in the editor, or with the Windows association when the path is null.
    /// Without an association it falls back to Notepad, so a first edit does not stop at the
    /// "How do you want to open this file?" prompt. Throws when the editor cannot start.
    /// </summary>
    public static void Open(string file, string? editorPath)
    {
        ClearElectronHostEnvironment();
        if (editorPath is not null)
        {
            var psi = new ProcessStartInfo(editorPath) { UseShellExecute = false };
            psi.ArgumentList.Add(file);
            Process.Start(psi);
            return;
        }

        if (HasAssociation(Path.GetExtension(file)))
        {
            try
            {
                Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
                return;
            }
            catch (Win32Exception)
            {
            }
        }
        Process.Start("notepad.exe", file);
    }

    /// <summary>
    /// Removes variables inherited when the Workbench is started from inside VS Code (its
    /// debugger or terminal). With ELECTRON_RUN_AS_NODE=1, Code.exe runs as plain Node and
    /// executes the script file as JavaScript instead of opening a window, and the VSCODE_*
    /// variables describe the host instance, not the one being started. They are cleared from
    /// this process, not only the child, because a shell-executed association inherits it as is.
    /// </summary>
    private static void ClearElectronHostEnvironment()
    {
        foreach (string name in Environment.GetEnvironmentVariables().Keys.Cast<string>().ToList())
        {
            if (name.Equals("ELECTRON_RUN_AS_NODE", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("VSCODE_", StringComparison.OrdinalIgnoreCase))
                Environment.SetEnvironmentVariable(name, null);
        }
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

    /// <summary>
    /// Code.exe from a per-user or machine-wide install, else the one behind code.cmd on PATH.
    /// </summary>
    private static string? FindVsCode()
    {
        string[] installs =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Microsoft VS Code", "Code.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft VS Code", "Code.exe"),
        ];
        if (installs.FirstOrDefault(File.Exists) is { } installed) return installed;

        // PATH holds "<install>\bin\code.cmd"; the editor itself is one folder up.
        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (dir.Length == 0 || !File.Exists(Path.Combine(dir, "code.cmd"))) continue;
            string exe = Path.GetFullPath(Path.Combine(dir, "..", "Code.exe"));
            if (File.Exists(exe)) return exe;
        }
        return null;
    }

    private static string? FindNotepadPlusPlus()
    {
        string[] installs =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Notepad++", "notepad++.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Notepad++", "notepad++.exe"),
        ];
        return installs.FirstOrDefault(File.Exists);
    }
}
