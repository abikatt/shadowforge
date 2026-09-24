using Microsoft.Win32;

namespace ShadowForge.Platform;

public static class ReblueInstallLocator
{
    private const string KeyPath = @"Software\Zolaware\reblue\Install";
    private const string ValueName = "InstallRoot";

    /// <summary>
    /// HKCU\Software\Zolaware\reblue\Install, value InstallRoot. Null when the key is absent
    /// or the platform is not Windows.
    /// </summary>
    public static string? GetInstallRoot()
    {
        if (!OperatingSystem.IsWindows())
            return null;
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName) as string;
    }
}
