using System.Numerics;
using ShadowForge.Formats.HDB;
using ShadowForge.Formats.HMB;

namespace ShadowForge.Tests.HMB;

public sealed class SamplerTests
{
    private const float AngleScale = 0.000095873802f;

    [Fact]
    public void LinearVec3_InterpolatesAndClamps()
    {
        var keys = new[]
        {
            new LinearVec3Key(0, new Vector3(0, 0, 0)),
            new LinearVec3Key(10, new Vector3(10, 20, 30)),
        };
        Assert.Equal(new Vector3(5, 10, 15), Sampler.SampleLinearVec3(keys, 5f));
        Assert.Equal(new Vector3(0, 0, 0), Sampler.SampleLinearVec3(keys, -1f));
        Assert.Equal(new Vector3(10, 20, 30), Sampler.SampleLinearVec3(keys, 99f));
    }

    [Fact]
    public void LinearEuler_WrapFix_TakesShortestPath()
    {
        var keys = new[]
        {
            new LinearEulerKey(0, 32752, 0, 0),
            new LinearEulerKey(2, unchecked((short)0x8010), 0, 0),
        };
        var (x, _, _) = Sampler.SampleLinearEuler(keys, 1f);
        Assert.Equal(32768f * AngleScale, x, 3);
    }

    [Fact]
    public void EulerToQuaternion_MatchesBindPoseMatrix()
    {
        var rnd = new Random(1234);
        for (int i = 0; i < 50; i++)
        {
            float x = (float)(rnd.NextDouble() * 4 - 2);
            float y = (float)(rnd.NextDouble() * 4 - 2);
            float z = (float)(rnd.NextDouble() * 4 - 2);
            var q = Sampler.EulerToQuaternion(x, y, z);
            var m = BindPose.EulerToMatrix3(x, y, z);
            foreach (var v in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(1, 2, 3) })
            {
                var byQuat = Vector3.Transform(v, q);
                var byMat = BindPose.Mat3Vec(m, v);
                Assert.True((byQuat - byMat).Length() < 1e-4f,
                    $"euler ({x},{y},{z}) vector {v}: quat {byQuat} != mat {byMat}");
            }
        }
    }

    [Fact]
    public void Hermite_EvaluatesCubicSegment()
    {
        var axis = new HermiteCurve
        {
            IsAngle = false,
            Keys = new[]
            {
                new HermiteKey(0, HalfBits(0f), 0f),
                new HermiteKey(30, HalfBits(1f), 0f),
            },
        };
        Assert.Equal(0f, Sampler.SampleHermite(axis, 0f), 4);
        Assert.Equal(0.5f, Sampler.SampleHermite(axis, 15f), 4);
        Assert.Equal(1f, Sampler.SampleHermite(axis, 30f), 4);
        Assert.Equal(1f, Sampler.SampleHermite(axis, 99f), 4);
    }

    [Fact]
    public void Hermite_TangentsArePerSecond()
    {
        var axis = new HermiteCurve
        {
            IsAngle = false,
            Keys = new[]
            {
                new HermiteKey(0, HalfBits(0f), 1f),
                new HermiteKey(30, HalfBits(0f), 0f),
            },
        };
        Assert.Equal(0.125f, Sampler.SampleHermite(axis, 15f), 4);
    }

    [Fact]
    public void Hermite_AngleTangents_AreScaledByAngleScale()
    {
        var axis = new HermiteCurve
        {
            IsAngle = true,
            Keys = new[]
            {
                new HermiteKey(0, 0, 1f / AngleScale),
                new HermiteKey(30, 0, 0f),
            },
        };
        Assert.Equal(0.125f, Sampler.SampleHermite(axis, 15f), 3);
    }

    [Fact]
    public void Hermite_AngleWrap_DegradesToLinear()
    {
        var axis = new HermiteCurve
        {
            IsAngle = true,
            Keys = new[]
            {
                new HermiteKey(0, 32752, 123f),
                new HermiteKey(1, unchecked((ushort)0x8010), 456f),
            },
        };
        float mid = Sampler.SampleHermite(axis, 0.5f);
        Assert.Equal(32768f * AngleScale, mid, 3);
    }

    [Fact]
    public void SampleTrack_ConstantChannels()
    {
        var track = new MotionTrack
        {
            BoneName = "b",
            Translation = new Vec3Channel { Linear = new[] { new LinearVec3Key(0, new Vector3(1, 2, 3)) } },
            Rotation = new EulerChannel { Linear = new[] { new LinearEulerKey(0, 0, 0, 16384) } },
            Scale = null,
        };
        var pose = Sampler.SampleTrack(track, 5f);
        Assert.Equal(new Vector3(1, 2, 3), pose.Translation!.Value);
        Assert.Null(pose.Scale);
        var expected = Sampler.EulerToQuaternion(0, 0, 16384 * AngleScale);
        Assert.True(Quaternion.Dot(pose.Rotation!.Value, expected) > 0.99999f);
    }

    private static ushort HalfBits(float f) => BitConverter.HalfToUInt16Bits((Half)f);
}
