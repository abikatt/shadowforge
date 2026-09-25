using System.Diagnostics;
using Microsoft.Win32;

namespace ShadowForge.Workbench;

/// <summary>
/// Starts Blender with a script that imports one character through the installed
/// io_shadowforge extension, the same path its Import Character operator takes. The
/// extension's own sforge and fresh workdir are used, so Deploy to Mod works afterwards.
/// </summary>
public static class BlenderLauncher
{
    private const string OpenScript = """
        import importlib
        import sys

        import addon_utils
        import bpy

        entity_id, game_root = sys.argv[sys.argv.index("--") + 1:][:2]


        def report(message):
            print("ShadowForge:", message)

            def draw(menu, _context):
                menu.layout.label(text=message)

            bpy.context.window_manager.popup_menu(draw, title="ShadowForge", icon="ERROR")


        def open_entity():
            name = next((m.__name__ for m in addon_utils.modules()
                         if m.__name__.rsplit(".", 1)[-1] == "io_shadowforge"), None)
            if name is None:
                report("The ShadowForge extension (io_shadowforge) is not installed in this Blender.")
                return None
            addon_utils.enable(name, default_set=True)
            bridge = importlib.import_module(name + ".bridge")
            scene = importlib.import_module(name + ".scene")
            prefs = bpy.context.preferences.addons[name].preferences
            try:
                exe = bridge.resolve_exe(prefs.sforge_path or None)
                for obj in list(bpy.data.objects):
                    bpy.data.objects.remove(obj)
                scene.open_blocking(entity_id, scene.fresh_workdir(entity_id), exe, game_root=game_root)
            except Exception as ex:
                report("Could not open %s: %s" % (entity_id, ex))
            return None


        bpy.app.timers.register(open_entity, first_interval=0.5)
        """;

    /// <summary>
    /// The Blender to use: the saved path if it still exists, else the .blend file association,
    /// else the newest version under Program Files\Blender Foundation, else Steam's copy.
    /// </summary>
    public static string? Find(string? saved)
    {
        if (saved is { Length: > 0 } && File.Exists(saved)) return saved;
        return FromFileAssociation() ?? FromProgramFiles() ?? FromSteam();
    }

    public static void OpenEntity(string blenderPath, string entityId, string gameRoot)
    {
        string dir = Path.Combine(Path.GetTempPath(), "ShadowForge");
        Directory.CreateDirectory(dir);
        string script = Path.Combine(dir, "workbench_open.py");
        File.WriteAllText(script, OpenScript);

        var psi = new ProcessStartInfo(blenderPath) { UseShellExecute = false };
        foreach (string arg in new[] { "--python", script, "--", entityId, gameRoot })
            psi.ArgumentList.Add(arg);
        Process.Start(psi);
    }

    private static string? FromFileAssociation()
    {
        if (!OperatingSystem.IsWindows()) return null;
        using var key = Registry.ClassesRoot.OpenSubKey(@"blendfile\shell\open\command");
        if (key?.GetValue(null) is not string command) return null;
        // "C:\...\blender-launcher.exe" "%1": the first quoted token is the executable.
        string exe = command.StartsWith('"') ? command[1..command.IndexOf('"', 1)] : command.Split(' ')[0];
        string sibling = Path.Combine(Path.GetDirectoryName(exe) ?? "", "blender.exe");
        return File.Exists(sibling) ? sibling : File.Exists(exe) ? exe : null;
    }

    private static string? FromProgramFiles()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Blender Foundation");
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateDirectories(root)
            .Select(d => Path.Combine(d, "blender.exe"))
            .Where(File.Exists)
            .OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string? FromSteam()
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam", "steamapps", "common", "Blender", "blender.exe");
        return File.Exists(path) ? path : null;
    }
}
