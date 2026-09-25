using System.Diagnostics;
using Microsoft.Win32;

namespace ShadowForge.Workbench;

/// <summary>
/// Starts Blender with a script that imports one character or map stage through the installed
/// io_shadowforge extension, the same paths its Import Character and Import Map operators take.
/// The extension's own sforge and fresh workdir are used, so Deploy to Mod works afterwards.
/// </summary>
public static class BlenderLauncher
{
    private const string OpenScript = """
        import importlib
        import sys

        import addon_utils
        import bpy

        kind, asset_id, game_root = sys.argv[sys.argv.index("--") + 1:][:3]


        def report(message):
            print("ShadowForge:", message)

            def draw(menu, _context):
                menu.layout.label(text=message)

            bpy.context.window_manager.popup_menu(draw, title="ShadowForge", icon="ERROR")


        def open_asset():
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
                workdir = scene.fresh_workdir(asset_id)
                if kind == "map":
                    res = bridge.export_map(exe, asset_id, workdir, game_root=game_root)
                    scene.import_map(bpy.context, workdir, asset_id, res)
                else:
                    scene.open_blocking(asset_id, workdir, exe, game_root=game_root)
            except Exception as ex:
                report("Could not open %s: %s" % (asset_id, ex))
            return None


        bpy.app.timers.register(open_asset, first_interval=0.5)
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

    public static void OpenEntity(string blenderPath, string entityId, string gameRoot) =>
        Open(blenderPath, "entity", entityId, gameRoot);

    public static void OpenMap(string blenderPath, string stageId, string gameRoot) =>
        Open(blenderPath, "map", stageId, gameRoot);

    private static void Open(string blenderPath, string kind, string assetId, string gameRoot)
    {
        string dir = Path.Combine(Path.GetTempPath(), "ShadowForge");
        Directory.CreateDirectory(dir);
        string script = Path.Combine(dir, "workbench_open.py");
        File.WriteAllText(script, OpenScript);

        var psi = new ProcessStartInfo(blenderPath) { UseShellExecute = false };
        foreach (string arg in new[] { "--python", script, "--", kind, assetId, gameRoot })
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
