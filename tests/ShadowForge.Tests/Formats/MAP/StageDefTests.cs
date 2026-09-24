using ShadowForge.Formats.MAP;

namespace ShadowForge.Tests.Formats.MAP;

public sealed class StageDefTests
{
    private static readonly string FixturePath =
        System.IO.Path.Combine(AppContext.BaseDirectory, "Formats/MAP/Fixtures/db_bg01_01.map");

    [Fact]
    public void RoundTrip_IsByteIdentical()
    {
        byte[] original = File.ReadAllBytes(FixturePath);
        var map = StageDef.Read(original);
        Assert.Equal(original, map.Write());
    }

    [Fact]
    public void Read_EnumeratesModelsAndParts()
    {
        var map = StageDef.ReadFile(FixturePath);
        Assert.Contains(map.Models, m => m.Name == "bg01_01fa.hdb");
        Assert.Contains(map.Models, m => m.ObjectHDB != null && m.ObjectHDB.EndsWith(".hdb"));
        Assert.Contains(map.Parts, p => p.Kind == "EFC");
        Assert.Contains(map.Parts, p => p.Kind == "CAM");
        Assert.Equal(24, map.Models.Count);
        Assert.Equal(8, map.Parts.Count);
    }

    [Fact]
    public void Read_LocksExactModelAndPartValues()
    {
        var map = StageDef.ReadFile(FixturePath);

        Assert.Contains(map.Models,
            m => m.Name == "bg01_01fa.hdb" && m.ObjectHDB == "map\\town\\bg01\\bg01_01fa.hdb");
        Assert.Contains(map.Models,
            m => m.Name == "bg01_01.hdb" && m.ObjectHDB == "map\\town\\bg01\\bg01_01.hdb");

        Assert.Contains(map.Parts,
            p => p.Kind == "EFC" && p.Path == "map\\town\\bg01\\bg01_01efc01.efc");

        Assert.Contains(map.Parts,
            p => p.Kind == "LOC" && p.Path == "map\\town\\bg01\\bg01_01_obs.hdb");
        Assert.Contains(map.Parts,
            p => p.Kind == "LOC" && p.Path == "map\\town\\bg01\\bg01_01_efs.hdb");
        Assert.Contains(map.Parts,
            p => p.Kind == "OCT" && p.Path == "map\\town\\bg01\\bg01_01c.hocb");
    }

    [Fact]
    public void Read_ParsesModelAttributesAndSettings()
    {
        var map = StageDef.ReadFile(FixturePath);
        var fa = map.Models.Single(m => m.Name == "bg01_01fa.hdb");
        Assert.Equal(0, fa.Area);
        Assert.Equal(0.0f, fa.Pri);
        Assert.Equal("ON", fa.Settings["VALIDFOG"]);
        Assert.Equal("OFF", fa.Settings["VALIDSHADOW"]);
        Assert.StartsWith("0.300000", fa.Settings["DIFFUSE"]);

        var sky = map.Models.Single(m => m.Name == "bg01_sky_a.hdb");
        Assert.Equal(-8.0f, sky.Pri);
    }
}
