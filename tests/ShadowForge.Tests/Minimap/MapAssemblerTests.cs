using ShadowForge.GameData;
using ShadowForge.Minimap;

namespace ShadowForge.Tests.Minimap;

public sealed class MapAssemblerTests
{
    [SkippableFact]
    public void AssemblesDg0503ToKnownBounds()
    {
        var tris = MapAssembler.AssembleStage(RetailData.Files, "dg05_03").Render;
        float minX = tris.Min(t => t.Min.X);
        float maxX = tris.Max(t => t.Max.X);
        float minZ = tris.Min(t => t.Min.Z);
        float maxZ = tris.Max(t => t.Max.Z);

        Assert.InRange(minX, -55f, -45f);
        Assert.InRange(maxX, 705f, 725f);
        Assert.InRange(maxZ - minZ, 280f, 320f);
    }

    [SkippableFact]
    public void SkipsLightingAndObstructionModels()
    {
        var log = new List<string>();
        MapAssembler.AssembleStage(RetailData.Files, "dg05_03", log.Add);
        Assert.Contains(log, l => l.Contains("skip") && l.Contains("lgh"));
    }

    [SkippableFact]
    public void SkipsEnvironmentModels()
    {
        var log = new List<string>();
        MapAssembler.AssembleStage(RetailData.Files, "bg01_01", log.Add);

        Assert.Contains(log, l => l.Contains("skip") && l.Contains("environment")
            && l.Contains("bg01_01fa.hdb"));
        Assert.Contains(log, l => l.Contains("skip") && l.Contains("environment")
            && l.Contains("bg01_sky_a.hdb"));
    }

    [SkippableFact]
    public void SkipsLightAndSpotRigModels()
    {
        var log = new List<string>();
        MapAssembler.AssembleStage(RetailData.Files, "bg24_01", log.Add);

        Assert.Contains(log, l => l.Contains("skip") && l.Contains("environment")
            && l.Contains("bg24_01lgt01.hdb"));
        Assert.Contains(log, l => l.Contains("skip") && l.Contains("environment")
            && l.Contains("bg24_01spt01.hdb"));
    }

    /// <summary>
    /// bg11_01b.hdb ships in both the bg11_01 and bg11_02 region packs. Stage bg11_02
    /// reads it from its own region.
    /// </summary>
    [SkippableFact]
    public void ReadsSharedModelFromTheStagesOwnRegion()
    {
        const string shared = @"map\town\bg11\bg11_01b.hdb";
        var files = RetailData.Files;

        var log = new List<string>();
        var tris = MapAssembler.AssembleStage(files, "bg11_02", log.Add).Render;

        Assert.DoesNotContain(log, l => l.Contains("error"));
        Assert.True(tris.Count > 0);
        Assert.Equal(new MapRegionReader(RetailData.Install, "bg11_02.ipk").Read(shared),
                     files.ReadStageAsset("bg11_02", shared));
    }
}
