using ShadowForge.Formats.HMB;
using ShadowForge.Formats.HMB.Import;

namespace ShadowForge.Tests.HMB;

public sealed class BuilderTests
{
    private const string Gm002 = "testdata/models/gimmick/gm002/gm002_01/gm002_01.hmb";

    [Fact]
    public void Build_Gm002_ByteIdenticalRoundTrip()
    {
        var original = File.ReadAllBytes(Gm002);
        var clip = MotionReader.Read(original, "gm002_01");
        var rebuilt = MotionBuilder.Build(clip);
        Assert.Equal(original, rebuilt);
    }

    [Fact]
    public void Build_ReadBack_PosesMatch()
    {
        var clip = MotionReader.Read(File.ReadAllBytes(Gm002), "gm002_01");
        var rebuilt = MotionReader.Read(MotionBuilder.Build(clip), "rebuilt");
        for (int f = 0; f <= clip.DurationFrames; f++)
            for (int t = 0; t < clip.Tracks.Count; t++)
            {
                var a = Sampler.SampleTrack(clip.Tracks[t], f);
                var b = Sampler.SampleTrack(rebuilt.Tracks[t], f);
                Assert.Equal(a.Translation, b.Translation);
                Assert.Equal(a.Rotation, b.Rotation);
                Assert.Equal(a.Scale, b.Scale);
            }
    }

    [Fact]
    public void Build_HermiteInput_Throws()
    {
        var clip = MotionReader.Read(File.ReadAllBytes("testdata/anim/bs01_bt_wt01.hmb"), "wt01");
        Assert.Throws<InvalidOperationException>(() => MotionBuilder.Build(clip));
    }

    [Fact]
    public void Build_NameTooLong_Throws()
    {
        var clip = new MotionClip { Name = "x", Mode = 2, DurationFrames = 1, Rate = 30f };
        clip.Tracks.Add(new MotionTrack
        {
            BoneName = "eighteen_char_name",
            Translation = new Vec3Channel
                { Linear = new[] { new LinearVec3Key(0, System.Numerics.Vector3.Zero) } },
        });
        Assert.Throws<InvalidDataException>(() => MotionBuilder.Build(clip));
    }

    [Fact]
    public void Validator_Wt01_BakedRoundTrip_WithinTolerance()
    {
        var clip = MotionReader.Read(File.ReadAllBytes("testdata/anim/bs01_bt_wt01.hmb"), "wt01");
        var baked = MotionValidator.BakeToLinear(clip);
        var rebuilt = MotionReader.Read(MotionBuilder.Build(baked), "rebuilt");
        var report = MotionValidator.Compare(clip, rebuilt);
        Assert.True(report.Passed, string.Join("; ", report.Messages)
            + $" T={report.MaxTranslationError} R={report.MaxRotationErrorRadians} S={report.MaxScaleError}");
    }
}
