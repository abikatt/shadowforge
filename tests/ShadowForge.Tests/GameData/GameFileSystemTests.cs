using System.Text;
using ShadowForge.Formats.IPK;
using ShadowForge.Formats.MDL;
using ShadowForge.GameData;

namespace ShadowForge.Tests.GameData;

public sealed class GameFileSystemTests
{
    [Fact]
    public void ReadVfs_FromLooseTree_ReturnsBytes()
    {
        var gfs = new GameFileSystem(GameDataFixture.Install());
        byte[] bytes = gfs.ReadVfs(@"chara\ene\em901\em901_obj.hdb");
        Assert.Equal("HDB", Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void ExistsVfs_LoosePresentAndAbsent()
    {
        var gfs = new GameFileSystem(GameDataFixture.Install());
        Assert.True(gfs.ExistsVfs(@"chara\ene\em901\em901_obj.hdb"));
        Assert.False(gfs.ExistsVfs(@"chara\ene\em901\does_not_exist.hdb"));
    }

    [Fact]
    public void Mount_WritesResolvedFilesToOutputDir()
    {
        var install = GameDataFixture.Install();
        var mdl = ModelDef.ReadFile(
            Path.Combine(GameDataFixture.Root, "database", "model", "chara", "ene", "model_em901.mdl"));
        var entity = new ResolvedEntity
        {
            Id = "em901", Class = "ene", RigId = "em901", RigClass = "ene", ModelDef = mdl,
            ModelDefRelPath = @"database\model\chara\ene\model_em901.mdl",
            Files = new List<ResolvedFile>
            {
                new(FileRole.Skeleton, @"chara\ene\em901\em901_obj.hdb",
                    @"chara\ipk\em901\ene\em901\em901_obj.hdb", "em901.ipk",
                    @"ene\em901\em901_obj.hdb", true),
            },
        };
        string outDir = GameDataFixture.TempPath("sf_mount_");
        int n = new GameFileSystem(install).Mount(entity, outDir);
        Assert.Equal(1, n);
        Assert.True(File.Exists(Path.Combine(outDir, @"chara\ipk\em901\ene\em901\em901_obj.hdb")));
    }

    [Fact]
    public void OwningIPK_DerivesCharaAndDatabasePacks()
    {
        var gfs = new GameFileSystem(GameDataFixture.Install());

        var chara = gfs.OwningIPK(@"chara\ene\em901\em901_obj.hdb");
        Assert.Equal("em901.ipk", chara.IPKName);
        Assert.Equal(@"ene\em901\em901_obj.hdb", chara.IPKInnerPath);

        var db = gfs.OwningIPK(@"database\model\chara\ply\model_pc01.mdl");
        Assert.Equal("database.ipk", db.IPKName);
        Assert.Equal(@"model\chara\ply\model_pc01.mdl", db.IPKInnerPath);
    }

    [Fact]
    public void ReadVfs_FromIPK_ExtractsInnerEntry()
    {
        string game = GameDataFixture.NewGameRoot("sf_ipk_");
        Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(game)!, "mods"));
        Directory.CreateDirectory(Path.Combine(game, "pack", "chara", "ipk"));
        var ipkBytes = ArchiveWriter.Build(new[]
        {
            new ArchiveWriter.InputEntry(@"ene\em901\em901_obj.hdb", "PACKEDHDB"u8.ToArray()),
        });
        File.WriteAllBytes(Path.Combine(game, "pack", "chara", "ipk", "em901.ipk"), ipkBytes);

        var gfs = new GameFileSystem(GameInstall.Locate(game));
        byte[] bytes = gfs.ReadVfs(@"chara\ene\em901\em901_obj.hdb");
        Assert.Equal("PACKEDHDB", Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void EnumerateVfs_FindsModelDefsInLooseTree()
    {
        var gfs = new GameFileSystem(GameDataFixture.Install());
        var hits = gfs.EnumerateVfs(@"database\model\chara", "model_*.mdl");
        Assert.Contains(@"database\model\chara\ene\model_em901.mdl", hits);
    }

    [Fact]
    public void EnumerateVfs_FindsModelDefsInDatabaseIPK()
    {
        string game = GameDataFixture.NewGameRoot("sf_enum_");
        Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(game)!, "mods"));
        Directory.CreateDirectory(Path.Combine(game, "pack"));
        var ipkBytes = ArchiveWriter.Build(new[]
        {
            new ArchiveWriter.InputEntry(@"model\chara\ply\model_pc01.mdl", "PATH pc01"u8.ToArray()),
        });
        File.WriteAllBytes(Path.Combine(game, "pack", "database.ipk"), ipkBytes);

        var gfs = new GameFileSystem(GameInstall.Locate(game));
        var hits = gfs.EnumerateVfs(@"database\model\chara", "model_*.mdl");
        Assert.Contains(@"database\model\chara\ply\model_pc01.mdl", hits);
    }

    [Fact]
    public void ReadVfs_MapPath_ResolvesThroughRegionManifest()
    {
        string game = GameDataFixture.NewGameRoot("sf_gfsmap_");
        Directory.CreateDirectory(Path.Combine(game, "pack", "map", "ipk"));
        File.WriteAllText(Path.Combine(game, "pack", "map", "pack_map_town.txt"),
            "\"db_bg01_01.map\",\"bg01_00.ipk\"\n\"db_bg06_01.map\",\"bg01_01.ipk\"\n");
        File.WriteAllBytes(Path.Combine(game, "pack", "map", "ipk", "bg01_01.ipk"),
            ArchiveWriter.Build(new[] {
                new ArchiveWriter.InputEntry(
                    @"map\town\bg01\only_in_01.hdb", "IN01"u8.ToArray()) }));

        var gfs = new GameFileSystem(GameInstall.Locate(game));
        Assert.True(gfs.ExistsVfs(@"map\town\bg01\only_in_01.hdb"));
        Assert.Equal("IN01", Encoding.ASCII.GetString(
            gfs.ReadVfs(@"map\town\bg01\only_in_01.hdb")));

        Assert.Equal("bg01_01.ipk", gfs.OwningIPK(@"map\town\bg01\only_in_01.hdb").IPKName);
        Assert.False(gfs.ExistsVfs(@"map\town\bg01\nowhere.hdb"));
        Assert.Throws<FileNotFoundException>(() => gfs.ReadVfs(@"map\town\bg01\nowhere.hdb"));
    }

    [Fact]
    public void ReadVfs_CharaPath_UsesManifestOwnedSharedIPK()
    {
        string game = GameDataFixture.NewGameRoot("sf_gfschara_");
        Directory.CreateDirectory(Path.Combine(game, "pack", "chara", "ipk"));
        File.WriteAllText(Path.Combine(game, "pack", "chara", "pack_chr_model.txt"),
            "\"model_em901.mdl\",\"shared.ipk\"\n");
        File.WriteAllBytes(Path.Combine(game, "pack", "chara", "ipk", "shared.ipk"),
            ArchiveWriter.Build(new[] {
                new ArchiveWriter.InputEntry(
                    @"ene\em901\em901_obj.hdb", "SHAREDHDB"u8.ToArray()) }));

        var gfs = new GameFileSystem(GameInstall.Locate(game));
        Assert.True(gfs.ExistsVfs(@"chara\ene\em901\em901_obj.hdb"));
        Assert.Equal("SHAREDHDB", Encoding.ASCII.GetString(
            gfs.ReadVfs(@"chara\ene\em901\em901_obj.hdb")));
        Assert.Equal("shared.ipk", gfs.OwningIPK(@"chara\ene\em901\em901_obj.hdb").IPKName);
    }

    [Fact]
    public void EnumerateVfs_MapDir_SweepsRegionPacksWithAndWithoutMapPrefix()
    {
        string game = GameDataFixture.NewGameRoot("sf_gfsenum_");
        Directory.CreateDirectory(Path.Combine(game, "pack", "map", "ipk"));
        File.WriteAllText(Path.Combine(game, "pack", "map", "pack_map_town.txt"),
            "\"db_bg01_01.map\",\"bg01_00.ipk\"\n\"db_bg06_01.map\",\"bg01_01.ipk\"\n");

        File.WriteAllBytes(Path.Combine(game, "pack", "map", "ipk", "bg01_00.ipk"),
            ArchiveWriter.Build(new[] {
                new ArchiveWriter.InputEntry(
                    @"map\town\bg01\keep.hdb", "K"u8.ToArray()) }));
        File.WriteAllBytes(Path.Combine(game, "pack", "map", "ipk", "bg01_01.ipk"),
            ArchiveWriter.Build(new[] {
                new ArchiveWriter.InputEntry(
                    @"town\bg01\drop.hdb", "D"u8.ToArray()) }));

        var hits = new GameFileSystem(GameInstall.Locate(game))
            .EnumerateVfs(@"map\town\bg01", "*.hdb");
        Assert.Contains(@"map\town\bg01\keep.hdb", hits);
        Assert.Contains(@"map\town\bg01\drop.hdb", hits);
    }
}
