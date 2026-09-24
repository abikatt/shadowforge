using ShadowForge.GameData;

namespace ShadowForge.Tests;

/// <summary>
/// The retail game data for tests that read shipped files, located the way the CLI
/// locates it: SHADOWFORGE_GAME_ROOT, else the reblue registry install. Either a
/// loose extract or a packed install works. Tests skip when no data is located.
/// </summary>
internal static class RetailData
{
    private static readonly Lazy<GameInstall?> Located = new(() =>
    {
        try { return GameInstall.Locate(); }
        catch (DirectoryNotFoundException) { return null; }
    });

    public static GameInstall Install
    {
        get
        {
            Skip.If(Located.Value is null,
                "No game data located. Set SHADOWFORGE_GAME_ROOT or install reblue.");
            return Located.Value!;
        }
    }

    public static GameFileSystem Files => new(Install);

    public static byte[] Read(string vfsPath)
    {
        var files = Files;
        Skip.IfNot(files.ExistsVfs(vfsPath), $"{vfsPath} is not in the located game data");
        return files.ReadVfs(vfsPath);
    }
}
