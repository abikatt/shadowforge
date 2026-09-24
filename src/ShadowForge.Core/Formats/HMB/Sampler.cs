using System.Numerics;

namespace ShadowForge.Formats.HMB;

/// <summary>
/// Evaluates tracks at a file frame. Frames outside the key range clamp to the end keys.
/// Angles interpolate along the shorter way around the 0x10000-step turn.
/// </summary>
public static class Sampler
{
    public static BonePose SampleTrack(MotionTrack track, float fileFrame)
    {
        Quaternion? rotation = null;
        if (track.Rotation is { } rc)
        {
            var (x, y, z) = SampleEuler(rc, fileFrame);
            rotation = EulerToQuaternion(x, y, z);
        }
        return new BonePose(
            track.Translation is { } tc ? SampleVec3(tc, fileFrame) : null,
            rotation,
            track.Scale is { } sc ? SampleVec3(sc, fileFrame) : null);
    }

    public static Vector3 SampleVec3(Vec3Channel channel, float frame)
    {
        if (channel.Linear is { } lin)
            return SampleLinearVec3(lin, frame);
        var axes = channel.Axes!;
        return new Vector3(
            SampleHermite(axes[0], frame),
            SampleHermite(axes[1], frame),
            SampleHermite(axes[2], frame));
    }

    /// <summary>
    /// Euler angles in radians.
    /// </summary>
    public static (float X, float Y, float Z) SampleEuler(EulerChannel channel, float frame)
    {
        if (channel.Linear is { } lin)
            return SampleLinearEuler(lin, frame);
        var axes = channel.Axes!;
        return (SampleHermite(axes[0], frame), SampleHermite(axes[1], frame), SampleHermite(axes[2], frame));
    }

    public static Vector3 SampleLinearVec3(LinearVec3Key[] keys, float frame)
    {
        if (keys.Length == 1) return keys[0].Value;
        int i = FindSegment(keys.Length, k => keys[k].Frame, frame, out float w);
        if (w <= 0f) return keys[i].Value;
        return Vector3.Lerp(keys[i].Value, keys[i + 1].Value, w);
    }

    public static (float X, float Y, float Z) SampleLinearEuler(LinearEulerKey[] keys, float frame)
    {
        int i = 0;
        float w = 0f;
        if (keys.Length > 1)
            i = FindSegment(keys.Length, k => keys[k].Frame, frame, out w);
        var a = keys[i];
        if (w <= 0f)
            return (RawAngle.ToRadians(a.X), RawAngle.ToRadians(a.Y), RawAngle.ToRadians(a.Z));
        var b = keys[i + 1];
        return (LerpAngle(a.X, b.X, w), LerpAngle(a.Y, b.Y, w), LerpAngle(a.Z, b.Z, w));
    }

    /// <summary>
    /// Key times are frame / 30 seconds regardless of the clip rate, and tangents are per second.
    /// A one-frame angle segment that wraps is interpolated linearly, ignoring its tangents.
    /// </summary>
    public static float SampleHermite(HermiteCurve axis, float fileFrame)
    {
        var keys = axis.Keys;
        if (keys.Length == 0) return 0f;
        float t = fileFrame / MotionClip.DefaultFrameRate;
        float first = keys[0].Frame / MotionClip.DefaultFrameRate;
        float last = keys[^1].Frame / MotionClip.DefaultFrameRate;
        if (t <= first + 1e-4f) return Decode(axis, keys[0].RawValue);
        if (t >= last) return Decode(axis, keys[^1].RawValue);

        int lo = 0, hi = keys.Length - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (keys[mid].Frame / MotionClip.DefaultFrameRate <= t) lo = mid; else hi = mid;
        }
        var pk = keys[lo];
        var nk = keys[hi];
        float t0 = pk.Frame / MotionClip.DefaultFrameRate, t1 = nk.Frame / MotionClip.DefaultFrameRate;
        if (MathF.Abs(t - t0) < 1e-4f) return Decode(axis, pk.RawValue);
        if (MathF.Abs(t - t1) < 1e-4f) return Decode(axis, nk.RawValue);

        float p0, p1, m0, m1;
        if (axis.IsAngle && nk.Frame - pk.Frame <= 1)
        {
            var (a, b) = Unwrap(unchecked((short)pk.RawValue), unchecked((short)nk.RawValue));
            p0 = RawAngle.ToRadians(a);
            p1 = RawAngle.ToRadians(b);
            float slope = (p1 - p0) / (t1 - t0);
            m0 = slope;
            m1 = slope;
        }
        else
        {
            p0 = Decode(axis, pk.RawValue);
            p1 = Decode(axis, nk.RawValue);
            m0 = axis.IsAngle ? RawAngle.ToRadians(pk.OutTangent) : pk.OutTangent;
            m1 = axis.IsAngle ? RawAngle.ToRadians(nk.OutTangent) : nk.OutTangent;
        }

        float dt = t1 - t0;
        float s = (t - t0) / dt;
        float s2 = s * s, s3 = s2 * s;
        return (2f * s3 - 3f * s2 + 1f) * p0
             + (3f * s2 - 2f * s3) * p1
             + dt * (s3 - 2f * s2 + s) * m0
             + dt * (s3 - s2) * m1;
    }

    /// <summary>
    /// Rotates about X, then Y, then Z.
    /// </summary>
    public static Quaternion EulerToQuaternion(float x, float y, float z)
    {
        var qx = Quaternion.CreateFromAxisAngle(Vector3.UnitX, x);
        var qy = Quaternion.CreateFromAxisAngle(Vector3.UnitY, y);
        var qz = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, z);
        return Quaternion.Concatenate(Quaternion.Concatenate(qx, qy), qz);
    }

    private static float LerpAngle(short rawA, short rawB, float w)
    {
        var (a, b) = Unwrap(rawA, rawB);
        return RawAngle.ToRadians(a + (b - a) * w);
    }

    /// <summary>
    /// Lifts one endpoint by a full turn when the two are more than half a turn apart.
    /// </summary>
    private static (int A, int B) Unwrap(int a, int b)
    {
        int d = b - a;
        if (d < -0x8000) b += 0x10000;
        else if (d > 0x8000) a += 0x10000;
        return (a, b);
    }

    private static int FindSegment(int count, Func<int, short> frameAt, float frame, out float w)
    {
        if (frame <= frameAt(0)) { w = 0f; return 0; }
        if (frame >= frameAt(count - 1)) { w = 0f; return count - 1; }
        int lo = 0, hi = count - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (frameAt(mid) <= frame) lo = mid; else hi = mid;
        }
        float f0 = frameAt(lo), f1 = frameAt(hi);
        w = f1 > f0 ? (frame - f0) / (f1 - f0) : 0f;
        return lo;
    }

    private static float Decode(HermiteCurve axis, ushort raw)
        => axis.IsAngle
            ? RawAngle.ToRadians(unchecked((short)raw))
            : (float)BitConverter.UInt16BitsToHalf(raw);
}
