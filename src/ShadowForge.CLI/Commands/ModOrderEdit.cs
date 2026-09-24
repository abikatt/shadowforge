using ShadowForge.GameData;
using ShadowForge.GameData.Mods;

namespace ShadowForge.CLI.Commands;

internal static class ModOrderEdit
{
    /// <summary>
    /// Adds or removes <paramref name="modName"/> in the target mod_order.txt and prints the
    /// result unless <paramref name="quiet"/> is set.
    /// </summary>
    public static void Apply(
        GameInstall install, string modName, bool enable, bool quiet,
        string? orderPath = null, string? profile = null)
    {
        var (path, other) = ModOrder.ResolveTarget(install, orderPath, profile);
        var order = ModOrder.Load(path);
        string message;
        if (enable)
        {
            order.Enable(modName);
            message = $"Enabled '{modName}' in {path}";
        }
        else
        {
            message = order.Disable(modName) ? $"Disabled '{modName}' in {path}" : $"'{modName}' was not in {path}";
        }
        order.Save();
        if (quiet) return;
        Console.WriteLine(message);
        if (other is not null)
            Console.WriteLine($"note: another mod_order.txt exists at {other} (not modified, pass --mod-order to 'mod enable' to target it)");
    }
}
