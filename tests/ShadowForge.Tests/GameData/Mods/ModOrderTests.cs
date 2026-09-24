using ShadowForge.GameData;
using ShadowForge.GameData.Mods;

namespace ShadowForge.Tests.GameData.Mods;

public sealed class ModOrderTests
{
    private static string TempFile() =>
        Path.Combine(GameDataFixture.NewTempDir("sf_order_"), "mod_order.txt");

    private static string MakeInstallRoot()
    {
        string install = GameDataFixture.TempPath("sf_inst_");
        Directory.CreateDirectory(Path.Combine(install, "game", "chara"));
        Directory.CreateDirectory(Path.Combine(install, "mods"));
        return install;
    }

    [Fact]
    public void Enable_AppendsOnce_LastWins_AndDedupes()
    {
        string path = TempFile();
        File.WriteAllText(path, "alpha\nbeta\n");
        var o = ModOrder.Load(path);
        o.Enable("beta");
        o.Enable("gamma");
        o.Save();
        Assert.Equal(new[] { "alpha", "beta", "gamma" }, ModOrder.Load(path).Names.ToArray());
    }

    [Fact]
    public void Disable_RemovesAllCasings_ReturnsWhetherPresent()
    {
        string path = TempFile();
        File.WriteAllText(path, "Alpha\nbeta\n");
        var o = ModOrder.Load(path);
        Assert.True(o.Disable("alpha"));
        Assert.False(o.Disable("missing"));
        o.Save();
        Assert.Equal(new[] { "beta" }, ModOrder.Load(path).Names.ToArray());
    }

    [Fact]
    public void Load_MissingFile_IsEmpty()
    {
        var o = ModOrder.Load(GameDataFixture.TempPath("sf_nofile_") + ".txt");
        Assert.Empty(o.Names);
    }

    [Fact]
    public void ResolveTarget_PrefersProfileFile_AndReportsRootAsOther()
    {
        string install = MakeInstallRoot();
        Directory.CreateDirectory(Path.Combine(install, "profiles", "default"));
        File.WriteAllText(Path.Combine(install, "mods", "mod_order.txt"), "root\n");
        File.WriteAllText(Path.Combine(install, "profiles", "default", "mod_order.txt"), "prof\n");

        var (chosen, other) = ModOrder.ResolveTarget(GameInstall.Locate(Path.Combine(install, "game")));
        Assert.Equal(Path.Combine(install, "profiles", "default", "mod_order.txt"), chosen);
        Assert.Equal(Path.Combine(install, "mods", "mod_order.txt"), other);
    }

    [Fact]
    public void ResolveTarget_ExplicitPathWins()
    {
        string install = MakeInstallRoot();
        string explicitPath = Path.Combine(GameDataFixture.TempPath("sf_custom_"), "mod_order.txt");

        var (chosen, other) = ModOrder.ResolveTarget(
            GameInstall.Locate(Path.Combine(install, "game")), explicitPath: explicitPath);
        Assert.Equal(explicitPath, chosen);
        Assert.Null(other);
    }
}
