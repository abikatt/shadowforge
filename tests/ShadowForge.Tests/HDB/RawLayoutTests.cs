using ShadowForge.Formats.HDB;
using ShadowForge.Formats.HDB.Raw;

namespace ShadowForge.Tests.HDB;

public sealed class RawLayoutTests
{
    [Fact]
    public void SemanticEquality_AfterStripAndCompute()
    {
        var bytes = File.ReadAllBytes(TestFile.HDB);
        var rawRef = ModelReader.Read(bytes);
        var rawComputed = ModelReader.Read(bytes);

        RawLayoutTestHelpers.StripLayout(rawComputed);
        RawLayout.Compute(rawComputed);

        Assert.True(
            RawLayoutTestHelpers.SemanticEqual(rawRef, rawComputed, out var diff),
            diff);
    }

    [Fact]
    public void CookedModelEquivalent_AfterStripAndCompute()
    {
        var bytes = File.ReadAllBytes(TestFile.HDB);
        var rawRef = ModelReader.Read(bytes);
        var rawComputed = ModelReader.Read(bytes);

        RawLayoutTestHelpers.StripLayout(rawComputed);
        RawLayout.Compute(rawComputed);

        var cookedRef = ModelCooker.Bake(rawRef);
        var cookedComputed = ModelCooker.Bake(rawComputed);

        RawLayoutTestHelpers.AssertCookedEquivalent(cookedRef, cookedComputed);
    }

    [Fact]
    public void SecondStripComputeWrite_MatchesFirst()
    {
        var bytes = File.ReadAllBytes(TestFile.HDB);

        var raw1 = ModelReader.Read(bytes);
        RawLayoutTestHelpers.StripLayout(raw1);
        RawLayout.Compute(raw1);
        var bytes1 = ModelWriter.Write(raw1);

        var raw2 = ModelReader.Read(bytes1);
        RawLayoutTestHelpers.StripLayout(raw2);
        RawLayout.Compute(raw2);
        var bytes2 = ModelWriter.Write(raw2);

        Assert.Equal(bytes1, bytes2);
    }
}
