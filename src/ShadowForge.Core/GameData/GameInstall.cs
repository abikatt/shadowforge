using ShadowForge.Platform;

namespace ShadowForge.GameData;

public enum GameDataSource { Loose, Install }

/// <summary>
/// The Blue Dragon game-data root: the folder holding database\, chara\, or default.xex.
/// A root is an Install when a mods\ folder sits beside it, otherwise a Loose extract.
/// </summary>
public sealed class GameInstall
{
    public string GameDataRoot { get; }
    public string? InstallRoot { get; }
    public string? ModsRoot { get; }
    public GameDataSource Source { get; }
    public IReadOnlyList<string> Overlays { get; private init; } = [];

    private GameInstall(string root, string? installRoot, string? modsRoot, GameDataSource source)
    {
        GameDataRoot = root;
        InstallRoot = installRoot;
        ModsRoot = modsRoot;
        Source = source;
    }

    /// <summary>
    /// A copy of this install with loose overlay roots searched ahead of it, first root first.
    /// </summary>
    public GameInstall WithOverlays(IEnumerable<string> roots)
    {
        var list = roots
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(TrimRoot)
            .ToList();
        return new GameInstall(GameDataRoot, InstallRoot, ModsRoot, Source) { Overlays = list };
    }

    /// <summary>
    /// An explicit root must be valid or this throws. Without one, SHADOWFORGE_GAME_ROOT is
    /// tried, then the game folder of the reblue registry install.
    /// </summary>
    public static GameInstall Locate(string? explicitRoot = null)
    {
        if (explicitRoot is { Length: > 0 })
        {
            string er = TrimRoot(explicitRoot);
            if (!IsValidRoot(er))
                throw new DirectoryNotFoundException(
                    $"Explicit game-data root is not valid (no database/chara/default.xex): {er}");
            return Classify(er);
        }
        foreach (string? candidate in AutoCandidates())
        {
            if (candidate is null) continue;
            string root = TrimRoot(candidate);
            if (IsValidRoot(root)) return Classify(root);
        }
        throw new DirectoryNotFoundException(
            "Could not locate a Blue Dragon game-data root (SHADOWFORGE_GAME_ROOT and the " +
            "reblue registry install were both absent/invalid). Pass --game-root explicitly.");
    }

    private static string TrimRoot(string root) => root.TrimEnd('\\', '/');

    private static IEnumerable<string?> AutoCandidates()
    {
        yield return Environment.GetEnvironmentVariable("SHADOWFORGE_GAME_ROOT");
        string? installRoot = ReblueInstallLocator.GetInstallRoot();
        yield return installRoot is null ? null : Path.Combine(installRoot, "game");
    }

    private static GameInstall Classify(string root)
    {
        string? parent = Path.GetDirectoryName(root);
        string? mods = parent is null ? null : Path.Combine(parent, "mods");
        bool isInstall = mods is not null && Directory.Exists(mods);
        return isInstall
            ? new GameInstall(root, parent, mods, GameDataSource.Install)
            : new GameInstall(root, null, null, GameDataSource.Loose);
    }

    private static bool IsValidRoot(string root) =>
        Directory.Exists(Path.Combine(root, "database"))
        || Directory.Exists(Path.Combine(root, "chara"))
        || File.Exists(Path.Combine(root, "default.xex"));
}
