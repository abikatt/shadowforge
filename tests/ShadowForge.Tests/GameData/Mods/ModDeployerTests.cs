using ShadowForge.GameData;
using ShadowForge.GameData.Mods;

namespace ShadowForge.Tests.GameData.Mods;

public sealed class ModDeployerTests
{
    private static (GameInstall install, string root) MakeInstall()
    {
        string root = GameDataFixture.TempPath("sf_inst_");
        string game = Path.Combine(root, "game");
        Directory.CreateDirectory(Path.Combine(root, "mods"));
        string mdlDir = Path.Combine(game, "database", "model", "chara", "ene");
        Directory.CreateDirectory(mdlDir);
        File.WriteAllText(Path.Combine(mdlDir, "model_em901.mdl"),
            "<ObjectData>{\r\n\tPATH\t\"chara\\ene\\em901\\\"\r\n\tOBJECT\t\"em901_obj.hdb\"\r\n\tMOTPACK\t\"em901_mot.mpk\"\r\n}\r\n");
        string charaDir = Path.Combine(game, "chara", "ene", "em901");
        Directory.CreateDirectory(charaDir);
        File.WriteAllBytes(Path.Combine(charaDir, "em901_obj.hdb"), "HDB"u8.ToArray());
        File.WriteAllBytes(Path.Combine(charaDir, "em901_mot.mpk"), "MPK"u8.ToArray());
        return (GameInstall.Locate(game), root);
    }

    private static string MakeEditedDir(params string[] names)
    {
        string dir = GameDataFixture.NewTempDir("sf_edit_");
        foreach (var n in names) File.WriteAllBytes(Path.Combine(dir, n), new byte[] { 9 });
        return dir;
    }

    [Fact]
    public void Deploy_WritesOverrideTreeUnderModName()
    {
        var (install, root) = MakeInstall();
        string edited = MakeEditedDir("a.bin", "b.bin");
        var plan = new PackPlan("em901", "em901", new[]
        {
            new PackEntry(Path.Combine(edited, "a.bin"), @"chara\ene\em901\em901_obj.hdb"),
            new PackEntry(Path.Combine(edited, "b.bin"), @"database\model\chara\ene\model_em901.mdl"),
        }, Array.Empty<string>());

        var result = new ModDeployer(install).Deploy(plan, "MyMod",
            new ModMetadata("MyMod", "tester", "0.1.0", "desc"));

        Assert.Equal(2, result.FilesWritten);
        Assert.True(File.Exists(Path.Combine(root, "mods", "MyMod", "chara", "ene", "em901", "em901_obj.hdb")));
        Assert.True(File.Exists(Path.Combine(root, "mods", "MyMod", "database", "model", "chara", "ene", "model_em901.mdl")));
        string toml = File.ReadAllText(Path.Combine(root, "mods", "MyMod", "mod.toml"));
        Assert.Contains("[mod]", toml);
        Assert.Contains("name = \"MyMod\"", toml);
        Assert.Contains("author = \"tester\"", toml);
    }

    [Fact]
    public void List_SkipsAccessLogsAndReportsEnabled()
    {
        var (install, root) = MakeInstall();
        Directory.CreateDirectory(Path.Combine(root, "mods", "Alpha"));
        Directory.CreateDirectory(Path.Combine(root, "mods", "mod_access_logs"));
        File.WriteAllText(Path.Combine(root, "mods", "mod_order.txt"), "Alpha\r\n");

        var mods = new ModDeployer(install).List();
        Assert.Contains(mods, m => m.Name == "Alpha" && m.Enabled);
        Assert.DoesNotContain(mods, m => m.Name == "mod_access_logs");
    }

    [Fact]
    public void Deploy_LooseInstall_Throws()
    {
        string loose = GameDataFixture.TempPath("sf_loose_");
        Directory.CreateDirectory(Path.Combine(loose, "chara"));
        var install = GameInstall.Locate(loose);
        var plan = new PackPlan("em901", "em901", new[] { new PackEntry("x", @"chara\ene\em901\x") }, Array.Empty<string>());
        Assert.Throws<InvalidOperationException>(() => new ModDeployer(install).Deploy(plan, "M"));
    }

    [Fact]
    public void EndToEnd_PackDeployEnable_ProducesManymaroStyleTreeAndOrder()
    {
        var (install, root) = MakeInstall();
        string edited = MakeEditedDir("em901_obj.hdb", "em901_mot.mpk", "em901_01.dds", "model_em901.mdl");

        var plan = new EntityPacker(install).BuildPlan("em901", edited);
        var result = new ModDeployer(install).Deploy(plan, "Replacer",
            new ModMetadata("Replacer", null, "1.0.0", null));

        Assert.Equal(4, result.FilesWritten);
        Assert.True(File.Exists(Path.Combine(root, "mods", "Replacer", "chara", "ene", "em901", "em901_01.dds")));
        Assert.True(File.Exists(Path.Combine(root, "mods", "Replacer", "database", "model", "chara", "ene", "model_em901.mdl")));

        var (orderPath, _) = ModOrder.ResolveTarget(install);
        var order = ModOrder.Load(orderPath);
        order.Enable("Replacer");
        order.Save();
        Assert.Contains("Replacer", ModOrder.Load(orderPath).Names);
    }
}
