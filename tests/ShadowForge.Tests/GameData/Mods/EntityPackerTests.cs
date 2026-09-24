using ShadowForge.GameData.Mods;

namespace ShadowForge.Tests.GameData.Mods;

public sealed class EntityPackerTests
{
    private static EntityPacker Packer() => new(GameDataFixture.Install());

    private static string MakeEditedDir(params string[] fileNames)
    {
        string dir = GameDataFixture.NewTempDir("sf_edit_");
        foreach (var n in fileNames) File.WriteAllBytes(Path.Combine(dir, n), new byte[] { 1, 2, 3 });
        return dir;
    }

    [Fact]
    public void BuildPlan_MapsEachFileToItsOverridePath()
    {
        string edited = MakeEditedDir("em901_obj.hdb", "em901_mot.mpk", "em901_01.dds", "model_em901.mdl");
        var plan = Packer().BuildPlan("em901", edited);

        Assert.Equal("em901", plan.EntityId);
        Assert.Equal(4, plan.Entries.Count);
        string Rel(string name) => plan.Entries.Single(e => e.SourcePath.EndsWith(name)).OverrideRelPath;
        Assert.Equal(@"chara\ene\em901\em901_obj.hdb", Rel("em901_obj.hdb"));
        Assert.Equal(@"chara\ene\em901\em901_mot.mpk", Rel("em901_mot.mpk"));
        Assert.Equal(@"chara\ene\em901\em901_01.dds",  Rel("em901_01.dds"));
        Assert.Equal(@"database\model\chara\ene\model_em901.mdl", Rel("model_em901.mdl"));
    }

    [Fact]
    public void BuildPlan_EmptyDir_Throws()
    {
        string edited = MakeEditedDir();
        Assert.Throws<InvalidOperationException>(() => Packer().BuildPlan("em901", edited));
    }

    [Fact]
    public void BuildPlan_MissingDir_Throws()
    {
        Assert.Throws<DirectoryNotFoundException>(() =>
            Packer().BuildPlan("em901", GameDataFixture.TempPath("sf_nope_")));
    }

    [Fact]
    public void BuildPlan_DuplicateOverrideTarget_Throws()
    {
        string dir = MakeEditedDir();
        Directory.CreateDirectory(Path.Combine(dir, "a"));
        Directory.CreateDirectory(Path.Combine(dir, "b"));
        File.WriteAllBytes(Path.Combine(dir, "a", "em901_01.dds"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(dir, "b", "em901_01.dds"), new byte[] { 2 });
        Assert.Throws<InvalidOperationException>(() => Packer().BuildPlan("em901", dir));
    }

    [Fact]
    public void BuildPlan_SkipsNonGameFiles_AndReportsThem()
    {
        string edited = MakeEditedDir();
        File.WriteAllText(Path.Combine(edited, "em901_obj.hdb"), "HDB");
        File.WriteAllText(Path.Combine(edited, "em901_obj.glb"), "GLB-EXPORT-LEFTOVER");
        File.WriteAllText(Path.Combine(edited, "em901.sfmod.json"), "{}");

        var plan = Packer().BuildPlan("em901", edited);

        Assert.Single(plan.Entries);
        Assert.EndsWith("em901_obj.hdb", plan.Entries[0].SourcePath);
        Assert.Equal(2, plan.Skipped.Count);
        Assert.Contains(plan.Skipped, s => s.EndsWith(".glb"));
        Assert.Contains(plan.Skipped, s => s.EndsWith(".sfmod.json"));
    }
}
