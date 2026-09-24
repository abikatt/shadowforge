using ShadowForge.Formats.HMB;

namespace ShadowForge.Tests.HMB;

public sealed class ReaderMode3Tests
{
    [Fact]
    public void BrDo_Mode3_ConstantChannelsDecodeAsLinearSingleKeys()
    {
        var clip = MotionReader.Read(File.ReadAllBytes("testdata/anim/bs01_br_do.hmb"), "bs01_br_do");
        Assert.Equal(3, clip.Mode);
        Assert.Equal(30f, clip.Rate);
        Assert.True(clip.Tracks.Count > 0);

        foreach (var t in clip.Tracks)
        {
            if (t.Translation is { Linear: not null } tl) Assert.Single(tl.Linear!);
            if (t.Rotation is { Linear: not null } rl) Assert.Single(rl.Linear!);
        }
    }

    [Fact]
    public void BtWt01_Mode3_HasHermiteAxes()
    {
        var clip = MotionReader.Read(File.ReadAllBytes("testdata/anim/bs01_bt_wt01.hmb"), "bs01_bt_wt01");
        Assert.Equal(3, clip.Mode);
        Assert.True(clip.DurationFrames > 10);

        Assert.Contains(clip.Tracks, t =>
            t.Rotation?.Axes != null && t.Rotation.Axes.Any(a => a.Keys.Length > 1));

        foreach (var t in clip.Tracks)
            if (t.Rotation?.Axes is { } axes)
                Assert.All(axes, a => Assert.True(a.IsAngle));

        foreach (var t in clip.Tracks)
            foreach (var axes in new[] { t.Translation?.Axes, t.Rotation?.Axes, t.Scale?.Axes })
                if (axes != null)
                    foreach (var axis in axes)
                        for (int k = 1; k < axis.Keys.Length; k++)
                            Assert.True(axis.Keys[k].Frame >= axis.Keys[k - 1].Frame);
    }

    [Fact]
    public void Pc02Rn01_RateFlagSemantics()
    {
        var clip = MotionReader.Read(File.ReadAllBytes("testdata/anim/pc02_fd_rn01.hmb"), "pc02_fd_rn01");
        Assert.Equal(300f, clip.Rate);
        Assert.True(clip.RateFlag > 0f);
        Assert.Equal(300f, clip.EffectiveRate);
        Assert.Equal(10, clip.FrameDivisor);
    }

    [Fact]
    public void Em057_ExtraSections_AreSurfaced()
    {
        var clip = MotionReader.Read(File.ReadAllBytes("testdata/anim/em057_fd_wk01.hmb"), "em057_fd_wk01");
        Assert.NotEmpty(clip.ExtraSectionTypes);
        Assert.NotEmpty(clip.Tracks);
    }
}
