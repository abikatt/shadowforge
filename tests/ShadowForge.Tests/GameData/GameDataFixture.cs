using ShadowForge.GameData;
using ShadowForge.GameData.Entities;

namespace ShadowForge.Tests.GameData;

internal static class GameDataFixture
{
    public static string Root => Path.Combine(AppContext.BaseDirectory, "GameData/Fixtures/gamedata");

    public static GameInstall Install() => GameInstall.Locate(Root);

    /// <summary>
    /// A unique path under the temp folder. The directory is not created.
    /// </summary>
    public static string TempPath(string prefix) =>
        Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));

    public static string NewTempDir(string prefix) =>
        Directory.CreateDirectory(TempPath(prefix)).FullName;

    /// <summary>
    /// A game root under a fresh temp install folder, with a chara\ folder so it locates.
    /// </summary>
    public static string NewGameRoot(string prefix)
    {
        string game = Path.Combine(TempPath(prefix), "game");
        Directory.CreateDirectory(Path.Combine(game, "chara"));
        return game;
    }

    public static ResolvedEntity ResolveWithMDL(string id, string mdlText)
    {
        string game = NewGameRoot("sf_mdlfix_");
        string mdlDir = Path.Combine(game, "database", "model", "chara", "ene");
        Directory.CreateDirectory(mdlDir);
        File.WriteAllText(Path.Combine(mdlDir, "model_" + id + ".mdl"), mdlText);
        return new EntityResolver(GameInstall.Locate(game)).Resolve(id);
    }
}
