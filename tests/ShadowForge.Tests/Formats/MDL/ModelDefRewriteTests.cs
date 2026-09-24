using System.Text;
using ShadowForge.Formats.MDL;

namespace ShadowForge.Tests.Formats.MDL;

public sealed class ModelDefRewriteTests
{
    private const string Source =
        "<ObjectData>\r\n" +
        "{\r\n" +
        "\tFACE\t\t\"chara\\npc\\np_face\\\"\r\n" +
        "\tPATH\t\t\"chara\\npc\\np109\\\"\r\n" +
        "\tOBJECT\t\t\"np109_obj.hdb\"\t// np109 note\r\n" +
        "\tMOTPACK\t\t\"np109_mot.mpk\"\r\n" +
        "\tMOTINPK\tFD_WT01\t\"np109_fd_wt02.hmb\"\t0.f,\r\n" +
        "\tMOTINPK\tFD_RN01\t\"np109_fd_rn01.hmb\"\t0.f,\r\n" +
        "\tMOTINPK\tnp109_SPECIAL\t\"np109_fd_sp01.hmb\"\t0.f,\r\n" +
        "}\r\n";

    private static ModelDef Parse() => ModelDef.Read(Encoding.ASCII.GetBytes(Source));

    [Fact]
    public void SetPath_RetargetsTheRigDirectory()
    {
        var mdl = Parse();
        mdl.SetPath(@"chara\ply\pc11\");
        Assert.Equal(@"chara\ply\pc11\", mdl.Path);
    }

    [Fact]
    public void RenameAssetToken_RewritesQuotedFileNames()
    {
        var mdl = Parse();
        int changed = mdl.RenameAssetToken("np109", "pc11");

        Assert.Equal("pc11_obj.hdb", mdl.ObjectHDB);
        Assert.Equal("pc11_mot.mpk", mdl.MotPack);
        Assert.Equal(new[] { "pc11_fd_wt02.hmb", "pc11_fd_rn01.hmb", "pc11_fd_sp01.hmb" },
            mdl.Clips.Select(c => c.HMBFile).ToArray());
        Assert.Equal(6, changed);
    }

    [Fact]
    public void RenameAssetToken_LeavesClipNamesAndFaceAlone()
    {
        var mdl = Parse();
        mdl.RenameAssetToken("np109", "pc11");

        Assert.Equal(new[] { "FD_WT01", "FD_RN01", "np109_SPECIAL" }, mdl.Clips.Select(c => c.Name).ToArray());
        Assert.Equal(@"chara\npc\np_face\", mdl.Face);
    }

    [Fact]
    public void RenameAssetToken_PreservesTrailingFlagsAndComments()
    {
        var mdl = Parse();
        mdl.RenameAssetToken("np109", "pc11");
        string text = Encoding.ASCII.GetString(mdl.Write());

        Assert.Contains("\tMOTINPK\tFD_WT01\t\"pc11_fd_wt02.hmb\"\t0.f,", text);
        Assert.Contains("\"pc11_obj.hdb\"\t// np109 note", text);
        Assert.Equal(Source.Split("\r\n").Length, text.Split("\r\n").Length);
    }

    [Fact]
    public void RenameAssetToken_UnchangedFileRoundTripsByteForByte()
    {
        var mdl = Parse();
        Assert.Equal(0, mdl.RenameAssetToken("em001", "em002"));
        Assert.Equal(Encoding.ASCII.GetBytes(Source), mdl.Write());
    }
}
