using System.Text;
using ShadowForge.GameData.Mods;

namespace ShadowForge.Tests.GameData.Mods;

public sealed class MotScrGeneratorTests
{
    private const string BaseTemplate =
        "pc00_at01,SET_MOTION,BT_AT01\r\n" +
        "pc00_sk01,SET_MOTION,BT_SK01\r\n" +
        "pc00_ra01,SET_MOTION,BT_RA01\r\n" +
        "pc00_mg01,SET_MOTION,BT_MG01\r\n" +
        "pc00_ca01,SET_MOTION,BT_CA01\r\n" +
        "pc00_cc01,SET_MOTION,BT_CC01\r\n" +
        "pc00_gs01,SET_MOTION,BT_GS01\r\n" +
        "pc00_it01,SET_MOTION,BT_IT01\r\n" +
        "pc00_ap01,SET_MOTION,BT_AP01\r\n" +
        "pc00_wi01,SET_MOTION,BT_WI01\r\n" +
        "pc00_mf01,SET_MOTION,BT_MF01\r\n" +
        "pc00_mb01,SET_MOTION,BT_MB01\r\n" +
        "pc00_dm01,SET_MOTION,BT_DM01\r\n" +
        "pc00_at01,SET_SILUET_MOTION,BT_AT01\r\n";

    private static MotScrGenerator GeneratorWithTemplate(byte[] template)
    {
        string overlay = GameDataFixture.NewTempDir("sf_mot_");
        string dir = Path.Combine(overlay, "database", "battle", "motscr");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "motscr_pc00.csv"), template);
        return new MotScrGenerator(GameDataFixture.Install().WithOverlays(new[] { overlay }));
    }

    private static string Generate() =>
        Encoding.ASCII.GetString(
            GeneratorWithTemplate(Encoding.ASCII.GetBytes(BaseTemplate)).Generate("pc00", "pc11"));

    [Fact]
    public void Generate_RenamesEverySection()
    {
        string result = Generate();

        Assert.Contains("pc11_at01,", result);
        Assert.DoesNotContain("pc00_", result);
    }

    [Fact]
    public void Generate_MapsAttackSectionsToTheTalkClip()
    {
        Assert.Contains("pc11_at01,SET_MOTION,FD_TK01B", Generate());
    }

    [Fact]
    public void Generate_MapsForwardAndBackMovement()
    {
        string result = Generate();

        Assert.Contains("pc11_mf01,SET_MOTION,FD_RN01", result);
        Assert.Contains("pc11_mb01,SET_MOTION,FD_WK01", result);
    }

    [Fact]
    public void Generate_MapsEverythingElseToTheIdleClip()
    {
        Assert.Contains("pc11_dm01,SET_MOTION,FD_WT01", Generate());
    }

    [Fact]
    public void Generate_LeavesSiluetMotionClipsAlone()
    {
        Assert.Contains("pc11_at01,SET_SILUET_MOTION,BT_AT01", Generate());
    }

    [Fact]
    public void Generate_PreservesShiftJISBytesFromTheTemplate()
    {
        byte[] template = Encoding.ASCII.GetBytes("pc00_at01,SET_MOTION,BT_AT01,")
            .Concat(new byte[] { 0x82, 0xa0, 0x82, 0xa2 })
            .Concat(Encoding.ASCII.GetBytes("\r\n"))
            .ToArray();

        byte[] result = GeneratorWithTemplate(template).Generate("pc00", "pc11");

        Assert.Contains("82A082A2", Convert.ToHexString(result));
    }

    [Theory]
    [InlineData("at")]
    [InlineData("sk")]
    [InlineData("ra")]
    [InlineData("mg")]
    [InlineData("ca")]
    [InlineData("cc")]
    [InlineData("gs")]
    [InlineData("it")]
    [InlineData("ap")]
    [InlineData("wi")]
    public void Generate_MapsAllActionGroupsToTheTalkClip(string group)
    {
        Assert.Contains($"pc11_{group}01,SET_MOTION,FD_TK01B", Generate());
    }
}
