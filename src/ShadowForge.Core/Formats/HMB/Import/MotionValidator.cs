using System.Numerics;
using ShadowForge.Formats.HDB.Import;

namespace ShadowForge.Formats.HMB.Import;

public static class MotionValidator
{
    /// <summary>
    /// Samples both clips through <see cref="Sampler"/> at every integer frame and reports the
    /// worst deviation per channel. Tracks are matched by position and must share bone names.
    /// </summary>
    public static ValidationReport Compare(
        MotionClip original, MotionClip rebuilt,
        float transTol = 1e-4f, float rotTolRad = 5e-4f, float scaleTol = 1e-4f)
    {
        var messages = new List<string>();
        float maxT = 0f, maxR = 0f, maxS = 0f;

        if (original.Tracks.Count != rebuilt.Tracks.Count)
        {
            messages.Add(
                $"track count mismatch: original={original.Tracks.Count} rebuilt={rebuilt.Tracks.Count}");
            return new ValidationReport(original.Name, 0f, 0f, 0f, messages, false);
        }

        for (int t = 0; t < original.Tracks.Count; t++)
        {
            var ta = original.Tracks[t];
            var tb = rebuilt.Tracks[t];
            if (!string.Equals(ta.BoneName, tb.BoneName, StringComparison.Ordinal))
            {
                messages.Add($"track {t} bone name mismatch: '{ta.BoneName}' vs '{tb.BoneName}'");
                continue;
            }

            for (int f = 0; f <= original.DurationFrames; f++)
            {
                var pa = Sampler.SampleTrack(ta, f);
                var pb = Sampler.SampleTrack(tb, f);

                if (pa.Translation.HasValue != pb.Translation.HasValue)
                    messages.Add($"{ta.BoneName} frame {f}: translation presence mismatch");
                else if (pa.Translation.HasValue)
                    KeepMax(ref maxT, Vector3.Distance(pa.Translation.Value, pb.Translation!.Value));

                if (pa.Rotation.HasValue != pb.Rotation.HasValue)
                    messages.Add($"{ta.BoneName} frame {f}: rotation presence mismatch");
                else if (pa.Rotation.HasValue)
                    KeepMax(ref maxR, (float)AngleBetween(pa.Rotation.Value, pb.Rotation!.Value));

                if (pa.Scale.HasValue != pb.Scale.HasValue)
                    messages.Add($"{ta.BoneName} frame {f}: scale presence mismatch");
                else if (pa.Scale.HasValue)
                    KeepMax(ref maxS, Vector3.Distance(pa.Scale.Value, pb.Scale!.Value));
            }
        }

        if (maxT > transTol) messages.Add($"translation error {maxT} exceeds tolerance {transTol}");
        if (maxR > rotTolRad) messages.Add($"rotation error {maxR} exceeds tolerance {rotTolRad}");
        if (maxS > scaleTol) messages.Add($"scale error {maxS} exceeds tolerance {scaleTol}");

        return new ValidationReport(original.Name, maxT, maxR, maxS, messages, messages.Count == 0);
    }

    /// <summary>
    /// Re-emits any clip as mode 2 with one key per integer frame 0..DurationFrames, so
    /// hermite clips can pass through the mode-2-only <see cref="MotionBuilder"/>.
    /// Translation and scale are copied as sampled floats. Rotation is sampled per euler axis
    /// and quantized with <see cref="HMBQuantize"/>, never through a quaternion: decomposing
    /// a quaternion back to euler loses the curve's unwrapped angle on fast-spinning bones.
    /// Angle quantization is then the only deviation from the source curve.
    /// </summary>
    public static MotionClip BakeToLinear(MotionClip clip)
    {
        var baked = new MotionClip
        {
            Name = clip.Name,
            Mode = 2,
            DurationFrames = clip.DurationFrames,
            Rate = clip.Rate,
            RateFlag = clip.RateFlag,
        };

        int frameCount = clip.DurationFrames + 1;

        foreach (var track in clip.Tracks)
        {
            var bakedTrack = new MotionTrack { BoneName = track.BoneName };

            if (track.Translation is { } tc)
                bakedTrack.Translation = BakeVec3(tc, frameCount);

            if (track.Scale is { } sc)
                bakedTrack.Scale = BakeVec3(sc, frameCount);

            if (track.Rotation is { } rc)
            {
                var anglesX = new float[frameCount];
                var anglesY = new float[frameCount];
                var anglesZ = new float[frameCount];
                for (int f = 0; f < frameCount; f++)
                    (anglesX[f], anglesY[f], anglesZ[f]) = Sampler.SampleEuler(rc, f);

                short[] rawX = HMBQuantize.QuantizeAngleAxis(anglesX);
                short[] rawY = HMBQuantize.QuantizeAngleAxis(anglesY);
                short[] rawZ = HMBQuantize.QuantizeAngleAxis(anglesZ);
                var rKeys = new LinearEulerKey[frameCount];
                for (int f = 0; f < frameCount; f++)
                    rKeys[f] = new LinearEulerKey((short)f, rawX[f], rawY[f], rawZ[f]);
                bakedTrack.Rotation = new EulerChannel { Linear = rKeys };
            }

            baked.Tracks.Add(bakedTrack);
        }

        return baked;
    }

    /// <summary>
    /// A NaN error is ignored, unlike <see cref="MathF.Max(float, float)"/>.
    /// </summary>
    private static void KeepMax(ref float max, float value)
    {
        if (value > max) max = value;
    }

    private static Vec3Channel BakeVec3(Vec3Channel channel, int frameCount)
    {
        var keys = new LinearVec3Key[frameCount];
        for (int f = 0; f < frameCount; f++)
            keys[f] = new LinearVec3Key((short)f, Sampler.SampleVec3(channel, f));
        return new Vec3Channel { Linear = keys };
    }

    /// <summary>
    /// Rotation angle between two quaternions, normalizing both, in double precision.
    /// </summary>
    private static double AngleBetween(Quaternion qa, Quaternion qb)
    {
        double dot = (double)qa.X * qb.X + (double)qa.Y * qb.Y + (double)qa.Z * qb.Z + (double)qa.W * qb.W;
        double na = Math.Sqrt((double)qa.X * qa.X + (double)qa.Y * qa.Y + (double)qa.Z * qa.Z + (double)qa.W * qa.W);
        double nb = Math.Sqrt((double)qb.X * qb.X + (double)qb.Y * qb.Y + (double)qb.Z * qb.Z + (double)qb.W * qb.W);
        return 2.0 * Math.Acos(Math.Min(1.0, Math.Abs(dot / (na * nb))));
    }
}
