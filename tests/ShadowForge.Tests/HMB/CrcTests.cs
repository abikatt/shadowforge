using ShadowForge.Formats.HMB;

namespace ShadowForge.Tests.HMB;

public sealed class CrcTests
{
    [Fact]
    public void Compute_CheckValue()
    {
        Assert.Equal(0xFC891918u, BoneNameCrc.Compute("123456789"));
    }

    [Fact]
    public void Compute_KnownBoneName()
    {
        Assert.Equal(0x83BE525Cu, BoneNameCrc.Compute("brow_r3"));
    }

    [Fact]
    public void Compute_EmptyString_IsComplementOfInit()
    {
        Assert.Equal(0x00000000u, BoneNameCrc.Compute(""));
    }
}
