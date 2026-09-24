using System.Text;
using ShadowForge.GameData.Mods;

namespace ShadowForge.Tests.GameData.Mods;

public sealed class ModSourceTests
{
    private static string MakeTree()
    {
        string root = GameDataFixture.NewTempDir("sf_src_");

        File.WriteAllText(Path.Combine(root, "sde.toml"),
            "[mod]\r\nname = \"sde\"\r\ndescription = \"Extra playable characters\"\r\n",
            Encoding.UTF8);

        string pc11 = Directory.CreateDirectory(
            Path.Combine(root, "entities", "pc11")).FullName;
        File.WriteAllText(Path.Combine(pc11, "entity.toml"),
            "[entity]\r\nid = \"pc11\"\r\nclass = \"ply\"\r\nname = \"King Jibral\"\r\n\r\n" +
            "[derive]\r\nfrom = \"np109\"\r\n\r\n" +
            "[generate.motscr]\r\ntemplate = \"pc00\"\r\n\r\n" +
            "[db]\r\n" +
            "\"!necessity/bd_gamedat/id_chr_model.csv\" = \",pc11,King Jibral,pc01,0,1,5,0,6,0,0,1\"\r\n" +
            "\"pack/chara/pack_chr_model.txt\" = '\"model_pc11.mdl\",\"np109.ipk\"'\r\n",
            Encoding.UTF8);
        File.WriteAllBytes(Path.Combine(pc11, "model_pc11.mdl"), new byte[] { 0x2F });
        return root;
    }

    [Fact]
    public void Load_ReadsTheModIdentity()
    {
        var src = ModSource.Load(MakeTree());

        Assert.Equal("sde", src.Name);
        Assert.Equal("Extra playable characters", src.Description);
    }

    [Fact]
    public void Load_ReadsEachEntitysIdentityAndDerivation()
    {
        var e = ModSource.Load(MakeTree()).Entities.Single();

        Assert.Equal("pc11", e.Id);
        Assert.Equal("ply", e.Class);
        Assert.Equal("King Jibral", e.Name);
        Assert.Equal("np109", e.DeriveFrom);
        Assert.Equal("pc00", e.MotScrTemplate);
    }

    [Fact]
    public void Load_ReadsDbRowsKeyedByVfsPath()
    {
        var e = ModSource.Load(MakeTree()).Entities.Single();

        Assert.Equal(",pc11,King Jibral,pc01,0,1,5,0,6,0,0,1",
            e.Db["!necessity/bd_gamedat/id_chr_model.csv"]);
        Assert.Equal("\"model_pc11.mdl\",\"np109.ipk\"",
            e.Db["pack/chara/pack_chr_model.txt"]);
    }

    [Fact]
    public void Load_RejectsAnEntityFolderWhoseIdDisagreesWithItsName()
    {
        string root = MakeTree();
        string toml = Path.Combine(root, "entities", "pc11", "entity.toml");
        File.WriteAllText(toml, File.ReadAllText(toml).Replace("id = \"pc11\"", "id = \"pc12\""));

        var ex = Assert.Throws<InvalidOperationException>(() => ModSource.Load(root));
        Assert.Contains("pc11", ex.Message);
        Assert.Contains("pc12", ex.Message);
    }

    [Fact]
    public void Load_RejectsARootWithNoModToml()
    {
        string empty = GameDataFixture.NewTempDir("sf_src_");

        Assert.Throws<FileNotFoundException>(() => ModSource.Load(empty));
    }

    [Fact]
    public void Load_RejectsARootWithMoreThanOneModToml()
    {
        string root = MakeTree();
        File.WriteAllText(Path.Combine(root, "other.toml"),
            "[mod]\r\nname = \"other\"\r\n", Encoding.UTF8);

        var ex = Assert.Throws<InvalidOperationException>(() => ModSource.Load(root));
        Assert.Contains("sde.toml", ex.Message);
        Assert.Contains("other.toml", ex.Message);
    }

    [Fact]
    public void Load_RejectsANonStringDbValue()
    {
        string root = MakeTree();
        string toml = Path.Combine(root, "entities", "pc11", "entity.toml");
        File.WriteAllText(toml, File.ReadAllText(toml) +
            "\"pack/chara/bad_row.txt\" = 5\r\n");

        var ex = Assert.Throws<InvalidOperationException>(() => ModSource.Load(root));
        Assert.Contains("pack/chara/bad_row.txt", ex.Message);
        Assert.Contains("pc11", ex.Message);
    }

    public static TheoryData<string> NotOneFolder => new()
    {
        "\\evil",
        Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "evil"),
        "..",
    };

    [Theory]
    [MemberData(nameof(NotOneFolder))]
    public void Load_RejectsAClassThatIsNotOneFolder(string cls)
    {
        string root = MakeTree();
        string toml = Path.Combine(root, "entities", "pc11", "entity.toml");
        File.WriteAllText(toml,
            File.ReadAllText(toml).Replace("class = \"ply\"", $"class = '{cls}'"));

        var ex = Assert.Throws<InvalidOperationException>(() => ModSource.Load(root));
        Assert.Contains("the class declared by entity 'pc11'", ex.Message);
    }
}
