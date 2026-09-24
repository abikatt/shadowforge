using System.Security.Cryptography;
using System.Text;
using ShadowForge.GameData;
using ShadowForge.GameData.Mods;

namespace ShadowForge.Tests.GameData.Mods;

public sealed class ModBuilderTests
{
    private static string NewTempDir() => GameDataFixture.NewTempDir("sf_bld_");

    private static string MakeVanillaRoot()
    {
        string root = NewTempDir();

        string mdlDir = Path.Combine(root, "database", "model", "chara", "npc");
        Directory.CreateDirectory(mdlDir);
        File.WriteAllText(Path.Combine(mdlDir, "model_np777.mdl"),
            "<ObjectData>\r\n{\r\n" +
            "\tPATH\t\t\"chara\\npc\\np777\\\"\r\n" +
            "\tOBJECT\t\t\"np777_obj.hdb\"\r\n" +
            "}\r\n", Encoding.ASCII);

        string rig = Path.Combine(root, "chara", "npc", "np777");
        Directory.CreateDirectory(rig);
        File.WriteAllBytes(Path.Combine(rig, "np777_obj.hdb"), TestModel.WithTextures("np777_01"));
        File.WriteAllBytes(Path.Combine(rig, "np777_01.dds"), new byte[] { 0xDD });

        string packDir = Path.Combine(root, "pack", "chara");
        Directory.CreateDirectory(packDir);
        File.WriteAllText(Path.Combine(packDir, "pack_chr_model.txt"),
            "\"model_pc01.mdl\",\"pc01.ipk\"\r\n", Encoding.ASCII);
        return root;
    }

    private static string MakeSource(string entityToml)
    {
        string root = NewTempDir();
        File.WriteAllText(Path.Combine(root, "sde.toml"),
            "[mod]\r\nname = \"sde\"\r\n", Encoding.UTF8);

        string pc11 = Directory.CreateDirectory(Path.Combine(root, "entities", "pc11")).FullName;
        File.WriteAllText(Path.Combine(pc11, "entity.toml"), entityToml, Encoding.UTF8);
        return root;
    }

    private static string DefaultEntityToml() =>
        "[entity]\r\nid = \"pc11\"\r\nclass = \"ply\"\r\nname = \"King Jibral\"\r\n\r\n" +
        "[derive]\r\nfrom = \"np777\"\r\n\r\n" +
        "[db]\r\n\"pack/chara/pack_chr_model.txt\" = '\"model_pc11.mdl\",\"np777.ipk\"'\r\n";

    private static (BuildResult Result, string ModsRoot) Build(string? entityToml = null)
    {
        var install = GameInstall.Locate(MakeVanillaRoot());
        string modsRoot = NewTempDir();
        var result = new ModBuilder(install)
            .Build(ModSource.Load(MakeSource(entityToml ?? DefaultEntityToml())), modsRoot);
        return (result, modsRoot);
    }

    [Fact]
    public void Build_WritesTheDerivedRigToItsOverridePath()
    {
        var (_, modsRoot) = Build();

        Assert.True(File.Exists(Path.Combine(modsRoot, "sde", "chara", "ply", "pc11", "pc11_obj.hdb")));
        Assert.True(File.Exists(Path.Combine(modsRoot, "sde", "chara", "ply", "pc11", "pc11_01.dds")));
    }

    [Fact]
    public void Build_WritesTheModelDefinitionToItsDatabasePath()
    {
        var (_, modsRoot) = Build();

        Assert.True(File.Exists(Path.Combine(
            modsRoot, "sde", "database", "model", "chara", "ply", "model_pc11.mdl")));
    }

    [Fact]
    public void Build_WritesTheMergedDbFile()
    {
        var (_, modsRoot) = Build();
        string text = File.ReadAllText(Path.Combine(
            modsRoot, "sde", "pack", "chara", "pack_chr_model.txt"));

        Assert.Contains("\"model_pc01.mdl\",\"pc01.ipk\"", text);
        Assert.Contains("\"model_pc11.mdl\",\"np777.ipk\"", text);
    }

    [Fact]
    public void Build_MergedDbFile_IsExactlyTheVanillaBasePlusTheDeclaredRow()
    {
        var (_, modsRoot) = Build();
        string text = File.ReadAllText(Path.Combine(
            modsRoot, "sde", "pack", "chara", "pack_chr_model.txt"));

        Assert.Equal(
            "\"model_pc01.mdl\",\"pc01.ipk\"\r\n\"model_pc11.mdl\",\"np777.ipk\"\r\n",
            text);
    }

    [Fact]
    public void Build_MergesDbRowsFromEveryEntityBeforeWritingOnce()
    {
        string root = NewTempDir();
        File.WriteAllText(Path.Combine(root, "sde.toml"),
            "[mod]\r\nname = \"sde\"\r\n", Encoding.UTF8);

        string pc11 = Directory.CreateDirectory(Path.Combine(root, "entities", "pc11")).FullName;
        File.WriteAllText(Path.Combine(pc11, "entity.toml"),
            "[entity]\r\nid = \"pc11\"\r\nclass = \"ply\"\r\nname = \"King Jibral\"\r\n\r\n" +
            "[derive]\r\nfrom = \"np777\"\r\n\r\n" +
            "[db]\r\n\"pack/chara/pack_chr_model.txt\" = '\"model_pc11.mdl\",\"np777.ipk\"'\r\n",
            Encoding.UTF8);

        string pc12 = Directory.CreateDirectory(Path.Combine(root, "entities", "pc12")).FullName;
        File.WriteAllText(Path.Combine(pc12, "entity.toml"),
            "[entity]\r\nid = \"pc12\"\r\nclass = \"ply\"\r\nname = \"Zola\"\r\n\r\n" +
            "[derive]\r\nfrom = \"np777\"\r\n\r\n" +
            "[db]\r\n\"pack/chara/pack_chr_model.txt\" = '\"model_pc12.mdl\",\"np777.ipk\"'\r\n",
            Encoding.UTF8);

        var install = GameInstall.Locate(MakeVanillaRoot());
        string modsRoot = NewTempDir();
        new ModBuilder(install).Build(ModSource.Load(root), modsRoot);

        string text = File.ReadAllText(Path.Combine(
            modsRoot, "sde", "pack", "chara", "pack_chr_model.txt"));

        Assert.Equal(1, CountOccurrences(text, "\"model_pc01.mdl\",\"pc01.ipk\""));
        Assert.Equal(1, CountOccurrences(text, "\"model_pc11.mdl\",\"np777.ipk\""));
        Assert.Equal(1, CountOccurrences(text, "\"model_pc12.mdl\",\"np777.ipk\""));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    [Fact]
    public void Build_WritesAModToml()
    {
        var (_, modsRoot) = Build();

        Assert.Contains("name = \"sde\"",
            File.ReadAllText(Path.Combine(modsRoot, "sde", "mod.toml")));
    }

    [Fact]
    public void Build_NeverWritesUnderTheGameDataRoot()
    {
        string vanillaRoot = MakeVanillaRoot();
        var before = SnapshotContent(vanillaRoot);

        var install = GameInstall.Locate(vanillaRoot);
        new ModBuilder(install).Build(ModSource.Load(MakeSource(DefaultEntityToml())), NewTempDir());

        var after = SnapshotContent(vanillaRoot);
        Assert.Equal(before, after);
    }

    private static Dictionary<string, string> SnapshotContent(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                p => p,
                p => Convert.ToBase64String(SHA256.HashData(File.ReadAllBytes(p))));

    [Fact]
    public void Build_ReportsEveryEntityItProduced()
    {
        var (result, _) = Build();

        Assert.Equal(new[] { "pc11" }, result.Entities);

        Assert.Equal(4, result.FilesWritten);
    }

    [Fact]
    public void Build_CopiesAnAuthoredModelDefinitionWithoutDeriving()
    {
        string root = NewTempDir();
        File.WriteAllText(Path.Combine(root, "sde.toml"),
            "[mod]\r\nname = \"sde\"\r\n", Encoding.UTF8);

        string pc13 = Directory.CreateDirectory(Path.Combine(root, "entities", "pc13")).FullName;
        File.WriteAllText(Path.Combine(pc13, "entity.toml"),
            "[entity]\r\nid = \"pc13\"\r\nclass = \"ply\"\r\nname = \"Authored Test\"\r\n",
            Encoding.UTF8);
        File.WriteAllText(Path.Combine(pc13, "model_pc13.mdl"),
            "<ObjectData>\r\n{\r\n" +
            "\tPATH\t\t\"chara\\ply\\pc13\\\"\r\n" +
            "\tOBJECT\t\t\"pc13_obj.hdb\"\r\n" +
            "}\r\n", Encoding.ASCII);
        File.WriteAllBytes(Path.Combine(pc13, "pc13_obj.hdb"), TestModel.WithTextures("pc13_01"));
        File.WriteAllBytes(Path.Combine(pc13, "pc13_01.dds"), new byte[] { 0xDD });

        var install = GameInstall.Locate(MakeVanillaRoot());
        string modsRoot = NewTempDir();
        new ModBuilder(install).Build(ModSource.Load(root), modsRoot);

        Assert.True(File.Exists(Path.Combine(
            modsRoot, "sde", "database", "model", "chara", "ply", "model_pc13.mdl")));
        Assert.True(File.Exists(Path.Combine(modsRoot, "sde", "chara", "ply", "pc13", "pc13_obj.hdb")));
        Assert.True(File.Exists(Path.Combine(modsRoot, "sde", "chara", "ply", "pc13", "pc13_01.dds")));
    }

    [Fact]
    public void Build_GeneratesAMotScrAndRoutesItToTheOverridePath()
    {
        string vanillaRoot = MakeVanillaRoot();
        string motscrDir = Path.Combine(vanillaRoot, "database", "battle", "motscr");
        Directory.CreateDirectory(motscrDir);
        File.WriteAllText(Path.Combine(motscrDir, "motscr_pc00.csv"),
            "pc00_at01,SET_MOTION,BT_AT01\r\n", Encoding.ASCII);

        string entityToml =
            "[entity]\r\nid = \"pc11\"\r\nclass = \"ply\"\r\nname = \"King Jibral\"\r\n\r\n" +
            "[derive]\r\nfrom = \"np777\"\r\n\r\n" +
            "[generate.motscr]\r\ntemplate = \"pc00\"\r\n\r\n" +
            "[db]\r\n\"pack/chara/pack_chr_model.txt\" = '\"model_pc11.mdl\",\"np777.ipk\"'\r\n";

        var install = GameInstall.Locate(vanillaRoot);
        string modsRoot = NewTempDir();
        new ModBuilder(install).Build(ModSource.Load(MakeSource(entityToml)), modsRoot);

        string path = Path.Combine(
            modsRoot, "sde", "database", "battle", "motscr", "motscr_pc11.csv");
        Assert.True(File.Exists(path));
        Assert.Contains("pc11_at01,SET_MOTION,FD_TK01B", File.ReadAllText(path));
    }

    [Fact]
    public void Build_RejectsAModNameThatIsNotASingleFolder()
    {
        string root = NewTempDir();
        File.WriteAllText(Path.Combine(root, "sde.toml"),
            "[mod]\r\nname = '../escaped'\r\n", Encoding.UTF8);

        var install = GameInstall.Locate(MakeVanillaRoot());
        var ex = Assert.Throws<InvalidOperationException>(
            () => new ModBuilder(install).Build(ModSource.Load(root), NewTempDir()));

        Assert.Contains("the mod name", ex.Message);
        Assert.Contains("../escaped", ex.Message);
    }

    [Fact]
    public void Build_RejectsADbKeyRootedAtTheDriveRoot()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(
            "[entity]\r\nid = \"pc11\"\r\nclass = \"ply\"\r\nname = \"King Jibral\"\r\n\r\n" +
            "[derive]\r\nfrom = \"np777\"\r\n\r\n" +
            "[db]\r\n'\\pack\\chara\\pack_chr_model.txt' = '\"model_pc11.mdl\",\"np777.ipk\"'\r\n"));

        Assert.Contains("the db key declared by entity 'pc11'", ex.Message);
        Assert.Contains("absolute", ex.Message);
    }

    [Fact]
    public void Build_RejectsADbKeyThatClimbsOutOfTheModFolder()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(
            "[entity]\r\nid = \"pc11\"\r\nclass = \"ply\"\r\nname = \"King Jibral\"\r\n\r\n" +
            "[derive]\r\nfrom = \"np777\"\r\n\r\n" +
            "[db]\r\n'../../game/pack/chara/pack_chr_model.txt' = " +
            "'\"model_pc11.mdl\",\"np777.ipk\"'\r\n"));

        Assert.Contains("the db key declared by entity 'pc11'", ex.Message);
        Assert.Contains("walks outside the mod folder", ex.Message);
    }

    [Fact]
    public void Build_ThrowsAClearMessageWhenNoModelDefinitionIsProduced()
    {
        string root = NewTempDir();
        File.WriteAllText(Path.Combine(root, "sde.toml"),
            "[mod]\r\nname = \"sde\"\r\n", Encoding.UTF8);

        string pc14 = Directory.CreateDirectory(Path.Combine(root, "entities", "pc14")).FullName;
        File.WriteAllText(Path.Combine(pc14, "entity.toml"),
            "[entity]\r\nid = \"pc14\"\r\nclass = \"ply\"\r\nname = \"Nobody\"\r\n",
            Encoding.UTF8);

        var install = GameInstall.Locate(MakeVanillaRoot());
        var ex = Assert.Throws<FileNotFoundException>(
            () => new ModBuilder(install).Build(ModSource.Load(root), NewTempDir()));

        Assert.Equal(
            "Entity 'pc14' produced no model_pc14.mdl. Declare [derive] or author one.",
            ex.Message);
    }
}
