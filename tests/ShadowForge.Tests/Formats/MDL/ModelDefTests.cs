using ShadowForge.Formats.MDL;

namespace ShadowForge.Tests.Formats.MDL;

public sealed class ModelDefTests
{
    private static readonly string FixturePath =
        System.IO.Path.Combine(AppContext.BaseDirectory, "Formats/MDL/Fixtures/model_pc01.mdl");

    [Fact]
    public void RoundTrip_IsByteIdentical()
    {
        byte[] original = File.ReadAllBytes(FixturePath);
        var mdl = ModelDef.Read(original);
        Assert.Equal(original, mdl.Write());
    }

    [Fact]
    public void Read_ExposesBindings()
    {
        var mdl = ModelDef.ReadFile(FixturePath);
        Assert.Equal("pc01_obj.hdb", mdl.ObjectHDB);
        Assert.Equal("pc01_mot.mpk", mdl.MotPack);
        Assert.Equal("chara\\ply\\pc01\\", mdl.Path);
        Assert.Contains(mdl.Clips, c => c.HMBFile.EndsWith(".hmb"));
        Assert.Contains(mdl.Clips, c => c.Name == "EV_WK01" && c.HMBFile == "ev000_pc01_wk01.hmb");
        Assert.Null(mdl.ObjectL0HDB);
    }

    [Fact]
    public void SetObjectHDB_RewritesOnlyThatBinding()
    {
        var mdl = ModelDef.ReadFile(FixturePath);
        mdl.SetObjectHDB("custom_obj.hdb");
        var reparsed = ModelDef.Read(mdl.Write());
        Assert.Equal("custom_obj.hdb", reparsed.ObjectHDB);
        Assert.Equal("pc01_mot.mpk", reparsed.MotPack);
    }

    [Fact]
    public void ObjectHDB_WithTextureOverrideExtras_ReturnsFirstQuotedToken()
    {
        byte[] bytes = "<ObjectData>\r\n{\r\n\tOBJECT\t\t\t\t\"em001_obj.hdb\" \"em072_tex.csv\"\r\n}\r\n"u8.ToArray();
        var mdl = ModelDef.Read(bytes);
        Assert.Equal("em001_obj.hdb", mdl.ObjectHDB);
    }

    [Fact]
    public void Read_SurfacesTextureOverrideCsv_TabSeparatedLayout()
    {
        var mdl = ModelDef.Read(System.Text.Encoding.ASCII.GetBytes(
            "<ObjectData>\n\tOBJECT\t\t\t\t\"sw03_obj.hdb\"\t\"sw03_tex_a.csv\"\n"));
        Assert.Equal("sw03_obj.hdb", mdl.ObjectHDB);
        Assert.Equal("sw03_tex_a.csv", mdl.TextureOverrideCsv);
    }

    [Fact]
    public void Read_SurfacesTextureOverrideCsv_SpaceSeparatedLayout()
    {
        var mdl = ModelDef.Read(System.Text.Encoding.ASCII.GetBytes(
            "\tOBJECT\t\t\t\t\"em001_obj.hdb\" \"em072_tex.csv\"\n"));
        Assert.Equal("em001_obj.hdb", mdl.ObjectHDB);
        Assert.Equal("em072_tex.csv", mdl.TextureOverrideCsv);
    }

    [Fact]
    public void Read_NoSecondToken_TextureOverrideCsvIsNull()
    {
        var mdl = ModelDef.Read(System.Text.Encoding.ASCII.GetBytes(
            "\tOBJECT\t\t\t\t\"em028_obj.hdb\"\n"));
        Assert.Null(mdl.TextureOverrideCsv);
    }

    [Fact]
    public void Read_TrailingCommentIsNotATextureOverride()
    {
        var mdl = ModelDef.Read(System.Text.Encoding.ASCII.GetBytes(
            "\tOBJECT\t\t\t\t\"em001_obj.hdb\"\t// \"quoted\" comment token\n"));
        Assert.Null(mdl.TextureOverrideCsv);
    }

    [Fact]
    public void Read_SurfacesFurLenFaceAndMotPacks()
    {
        var mdl = ModelDef.Read(System.Text.Encoding.ASCII.GetBytes(
            "\tFURLEN\t0.4 1.0\t\t\t\t// length note\n" +
            "\tFACE\t\t\t\t\"chara\\npc\\np_face\\\"\t\n" +
            "\tMOTPACK\t\t\t\t\"np005_mot.mpk\"\n" +
            "\tMOTPACK_F\t\t\t\"npb01_fc_mot.mpk\"\n"));
        Assert.Equal("0.4 1.0", mdl.FurLen);
        Assert.Equal(@"chara\npc\np_face\", mdl.Face);
        Assert.Equal("npb01_fc_mot.mpk", mdl.MotPackF);
        Assert.Null(mdl.MotPackB);
    }

    [Fact]
    public void Read_SurfacesObjectOpts()
    {
        var mdl = ModelDef.Read(System.Text.Encoding.ASCII.GetBytes(
            "\tOBJECT\t\t\t\t\"em028_obj.hdb\"\n" +
            "\tOBJECTOPT\t\t\t0 \"em028_efc_eye_obj.hdb\"\t\t// blink eye\n"));
        var opt = Assert.Single(mdl.ObjectOpts);
        Assert.Equal(0, opt.Slot);
        Assert.Equal("em028_efc_eye_obj.hdb", opt.HDBFile);
    }

    [Fact]
    public void Read_SurfacesBareMotionLines()
    {
        var mdl = ModelDef.Read(System.Text.Encoding.ASCII.GetBytes(
            "\tMOTION\t\tFD_WK01\t\t\"mt09_fd_fl01.hmb\"\t\t// fin default\n" +
            "\tMOTION\t\tFD_WK02\t\t\"mt09_fd_fl02.hmb\"\n"));
        Assert.Equal(2, mdl.Motions.Count);
        Assert.Equal("FD_WK01", mdl.Motions[0].Name);
        Assert.Equal("mt09_fd_fl01.hmb", mdl.Motions[0].HMBFile);
    }
}
