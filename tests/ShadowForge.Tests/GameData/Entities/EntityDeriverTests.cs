using System.Text;
using ShadowForge.Formats.HDB;
using ShadowForge.Formats.HDB.Raw;
using ShadowForge.Formats.IPK;
using ShadowForge.Formats.MDL;
using ShadowForge.GameData;
using ShadowForge.GameData.Entities;

namespace ShadowForge.Tests.GameData.Entities;

public sealed class EntityDeriverTests
{
    private static string NewTempDir() => GameDataFixture.NewTempDir("sf_drv_");

    private static string MakeSourceRoot()
    {
        string root = NewTempDir();

        string mdlDir = Path.Combine(root, "database", "model", "chara", "npc");
        Directory.CreateDirectory(mdlDir);
        File.WriteAllText(Path.Combine(mdlDir, "model_np777.mdl"),
            "<ObjectData>\r\n{\r\n" +
            "\tFACE\t\t\"chara\\npc\\np_face\\\"\r\n" +
            "\tPATH\t\t\"chara\\npc\\np777\\\"\r\n" +
            "\tOBJECT\t\t\"np777_obj.hdb\"\r\n" +
            "\tMOTPACK\t\t\"np777_mot.mpk\"\r\n" +
            "\tMOTINPK\tFD_WT01\t\"np777_fd_wt01.hmb\"\t0.f,\r\n" +
            "}\r\n", Encoding.ASCII);

        string rig = Path.Combine(root, "chara", "npc", "np777");
        Directory.CreateDirectory(rig);
        File.WriteAllBytes(Path.Combine(rig, "np777_obj.hdb"), TestModel.WithTextures("np777_01"));
        File.WriteAllBytes(Path.Combine(rig, "np777_mot.mpk"),
            ArchiveWriter.Build(new[]
            {
                new ArchiveWriter.InputEntry(
                    "np777_fd_wt01.hmb", Encoding.ASCII.GetBytes("CLIP")),
            }));
        File.WriteAllBytes(Path.Combine(rig, "np777_01.dds"), new byte[] { 0xDD, 0x53 });
        return root;
    }

    private static EntityDeriver Deriver(string sourceRoot) =>
        new(GameInstall.Locate(sourceRoot));

    [Fact]
    public void Derive_RenamesEveryRigFile()
    {
        string outDir = NewTempDir();
        var result = Deriver(MakeSourceRoot()).Derive("np777", "pc11", "ply", outDir);

        var names = result.Files.Select(Path.GetFileName).OrderBy(n => n).ToArray();
        Assert.Equal(
            new[] { "model_pc11.mdl", "pc11_01.dds", "pc11_mot.mpk", "pc11_obj.hdb" },
            names);
    }

    [Fact]
    public void Derive_RetargetsTheModelDefinition()
    {
        string outDir = NewTempDir();
        Deriver(MakeSourceRoot()).Derive("np777", "pc11", "ply", outDir);

        var mdl = ModelDef.ReadFile(Directory
            .EnumerateFiles(outDir, "model_pc11.mdl", SearchOption.AllDirectories).Single());

        Assert.Equal(@"chara\ply\pc11\", mdl.Path);
        Assert.Equal("pc11_obj.hdb", mdl.ObjectHDB);
        Assert.Equal("pc11_mot.mpk", mdl.MotPack);
        Assert.Equal("pc11_fd_wt01.hmb", mdl.Clips.Single().HMBFile);
        Assert.Equal(@"chara\npc\np_face\", mdl.Face);
    }

    [Fact]
    public void Derive_RewritesTheHDBTextureTable()
    {
        string outDir = NewTempDir();
        Deriver(MakeSourceRoot()).Derive("np777", "pc11", "ply", outDir);

        string hdb = Directory
            .EnumerateFiles(outDir, "pc11_obj.hdb", SearchOption.AllDirectories).Single();
        var table = ModelReader.Read(File.ReadAllBytes(hdb))
            .FirstTable.OfType<RawTextureTableEntry>().Single();

        Assert.Equal("pc11_01", table.Records.Single().Name);
    }

    [Fact]
    public void Derive_RewritesTheMotionPackToc()
    {
        string outDir = NewTempDir();
        Deriver(MakeSourceRoot()).Derive("np777", "pc11", "ply", outDir);

        string mpk = Directory
            .EnumerateFiles(outDir, "pc11_mot.mpk", SearchOption.AllDirectories).Single();
        using var ms = new MemoryStream(File.ReadAllBytes(mpk));
        var archive = ArchiveReader.ReadArchive(ms);

        Assert.Equal("pc11_fd_wt01.hmb", archive.Entries.Single().Name);
    }

    [Fact]
    public void Derive_RejectsANameThatWouldOverflowTheTextureField()
    {
        string root = MakeSourceRoot();
        string hdb = Path.Combine(root, "chara", "npc", "np777", "np777_obj.hdb");
        File.WriteAllBytes(hdb, TestModel.WithTextures("np777_eyelid_l_0"));

        var ex = Assert.Throws<ArgumentException>(() =>
            Deriver(root).Derive("np777", "pc111111111", "ply", NewTempDir()));
        Assert.Contains("20", ex.Message);
    }

    [Fact]
    public void Derive_PreservesShiftJISBytesInARigCsv()
    {
        string root = MakeSourceRoot();
        File.WriteAllBytes(Path.Combine(root, "chara", "npc", "np777", "np777_tex.csv"),
            Encoding.ASCII.GetBytes("np777_01,")
                .Concat(new byte[] { 0x82, 0xa0, 0x82, 0xa2 })
                .Concat(Encoding.ASCII.GetBytes("\r\n"))
                .ToArray());

        string outDir = NewTempDir();
        Deriver(root).Derive("np777", "pc11", "ply", outDir);

        byte[] csv = File.ReadAllBytes(Path.Combine(outDir, "pc11_tex.csv"));
        Assert.Equal("pc11_01,", Encoding.ASCII.GetString(csv, 0, 8));
        Assert.Contains("82A082A2", Convert.ToHexString(csv));
    }

    [Fact]
    public void Derive_ReadOnlySourceFile_SucceedsAndCleansUpStaging()
    {
        string root = MakeSourceRoot();
        string hdb = Path.Combine(root, "chara", "npc", "np777", "np777_obj.hdb");
        File.SetAttributes(hdb, File.GetAttributes(hdb) | FileAttributes.ReadOnly);

        string outDir = NewTempDir();
        var result = Deriver(root).Derive("np777", "pc11", "ply", outDir);

        var names = result.Files.Select(Path.GetFileName).OrderBy(n => n).ToArray();
        Assert.Equal(
            new[] { "model_pc11.mdl", "pc11_01.dds", "pc11_mot.mpk", "pc11_obj.hdb" },
            names);
        Assert.False(Directory.Exists(Path.Combine(outDir, ".rig")));
    }

    [Fact]
    public void Derive_ReadOnlySourceFile_StillSurfacesTheOverflowException()
    {
        string root = MakeSourceRoot();
        string hdb = Path.Combine(root, "chara", "npc", "np777", "np777_obj.hdb");
        File.WriteAllBytes(hdb, TestModel.WithTextures("np777_eyelid_l_0"));
        File.SetAttributes(hdb, File.GetAttributes(hdb) | FileAttributes.ReadOnly);

        var ex = Assert.Throws<ArgumentException>(() =>
            Deriver(root).Derive("np777", "pc111111111", "ply", NewTempDir()));
        Assert.Contains("20", ex.Message);
    }
}
