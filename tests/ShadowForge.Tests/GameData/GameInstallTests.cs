using ShadowForge.GameData;

namespace ShadowForge.Tests.GameData;

public sealed class GameInstallTests
{
    private static string MakeRoot(bool asInstall)
    {
        string tmp = GameDataFixture.TempPath("sf_gi_");
        string game = asInstall ? Path.Combine(tmp, "game") : tmp;
        Directory.CreateDirectory(Path.Combine(game, "database"));
        Directory.CreateDirectory(Path.Combine(game, "chara"));
        if (asInstall) Directory.CreateDirectory(Path.Combine(tmp, "mods"));
        return game;
    }

    [Fact]
    public void Locate_ExplicitLooseRoot_ClassifiesLoose()
    {
        string root = MakeRoot(asInstall: false);
        var gi = GameInstall.Locate(root);
        Assert.Equal(root, gi.GameDataRoot);
        Assert.Equal(GameDataSource.Loose, gi.Source);
        Assert.Null(gi.ModsRoot);
    }

    [Fact]
    public void Locate_ExplicitInstallGameRoot_ClassifiesInstallAndFindsMods()
    {
        string game = MakeRoot(asInstall: true);
        var gi = GameInstall.Locate(game);
        Assert.Equal(GameDataSource.Install, gi.Source);
        Assert.NotNull(gi.ModsRoot);
        Assert.True(Directory.Exists(gi.ModsRoot!));
    }

    [Fact]
    public void Locate_InvalidRoot_Throws()
    {
        string empty = GameDataFixture.NewTempDir("sf_gi_empty_");
        Assert.Throws<DirectoryNotFoundException>(() => GameInstall.Locate(empty));
    }
}
