using System.Numerics;
using ShadowForge.Formats.HMB;

namespace ShadowForge.Formats.HDB.Import;

/// <summary>
/// Packs sampled animation into HMB linear keys: euler angles unwrapped and quantized to
/// raw shorts, then frames dropped greedily while linear interpolation stays within
/// tolerance.
/// </summary>
internal static class HMBQuantize
{
    private const float Vec3Tolerance = 1e-4f;
    private const float AngleToleranceRaw = 2f;

    /// <summary>
    /// Unwraps per-frame angles (radians) along the shortest path from the previous frame,
    /// then packs each to a raw HMB short: round(angle / RawAngle.Scale) mod 0x10000. More
    /// than half a turn per frame cannot be told apart from its shortest-path alias in
    /// quaternion samples, and the runtime decodes the alias too.
    /// </summary>
    internal static short[] QuantizeAngleAxis(float[] anglesRadians)
    {
        var raw = new short[anglesRadians.Length];
        float unwrapped = anglesRadians[0];
        raw[0] = ToRawShort(unwrapped);
        for (int i = 1; i < anglesRadians.Length; i++)
        {
            float angle = anglesRadians[i];
            while (angle - unwrapped > MathF.PI) angle -= 2f * MathF.PI;
            while (angle - unwrapped < -MathF.PI) angle += 2f * MathF.PI;
            unwrapped = angle;
            raw[i] = ToRawShort(angle);
        }
        return raw;
    }

    private static short ToRawShort(float angleRadians)
        => (short)(((int)MathF.Round(angleRadians / RawAngle.Scale)) & 0xFFFF);

    internal static LinearVec3Key[] ReduceVec3(Vector3[] values)
    {
        int n = values.Length;
        bool constant = true;
        for (int i = 1; i < n && constant; i++)
            if (!WithinVec3Tolerance(values[0], values[i])) constant = false;
        if (constant)
            return new[] { new LinearVec3Key(0, values[0]) };

        var kept = SelectKeptFrames(n, (a, mid, end) =>
            WithinVec3Tolerance(Vector3.Lerp(values[a], values[end], Weight(a, mid, end)), values[mid]));

        var result = new LinearVec3Key[kept.Count];
        for (int i = 0; i < kept.Count; i++)
            result[i] = new LinearVec3Key((short)kept[i], values[kept[i]]);
        return result;
    }

    internal static LinearEulerKey[] ReduceEuler(short[] rawX, short[] rawY, short[] rawZ)
    {
        int n = rawX.Length;
        bool constant = true;
        for (int i = 1; i < n && constant; i++)
        {
            if (!WithinAngleTolerance(rawX[i], rawX[0]) ||
                !WithinAngleTolerance(rawY[i], rawY[0]) ||
                !WithinAngleTolerance(rawZ[i], rawZ[0]))
                constant = false;
        }
        if (constant)
            return new[] { new LinearEulerKey(0, rawX[0], rawY[0], rawZ[0]) };

        var kept = SelectKeptFrames(n, (a, mid, end) =>
        {
            float w = Weight(a, mid, end);
            return WithinAngleTolerance(rawX[mid], LerpAngleRaw(rawX[a], rawX[end], w)) &&
                   WithinAngleTolerance(rawY[mid], LerpAngleRaw(rawY[a], rawY[end], w)) &&
                   WithinAngleTolerance(rawZ[mid], LerpAngleRaw(rawZ[a], rawZ[end], w));
        });

        var result = new LinearEulerKey[kept.Count];
        for (int i = 0; i < kept.Count; i++)
        {
            int f = kept[i];
            result[i] = new LinearEulerKey((short)f, rawX[f], rawY[f], rawZ[f]);
        }
        return result;
    }

    private static List<int> SelectKeptFrames(int frameCount, Func<int, int, int, bool> fitsWithinTolerance)
    {
        var kept = new List<int> { 0 };
        int anchor = 0;
        while (anchor < frameCount - 1)
        {
            int end = anchor + 1;
            for (int candidate = anchor + 2; candidate <= frameCount - 1; candidate++)
            {
                bool ok = true;
                for (int mid = anchor + 1; mid < candidate; mid++)
                {
                    if (!fitsWithinTolerance(anchor, mid, candidate)) { ok = false; break; }
                }
                if (!ok) break;
                end = candidate;
            }
            kept.Add(end);
            anchor = end;
        }
        return kept;
    }

    private static float Weight(int anchor, int mid, int end) => (mid - anchor) / (float)(end - anchor);

    private static bool WithinVec3Tolerance(Vector3 a, Vector3 b)
        => MathF.Abs(a.X - b.X) <= Vec3Tolerance
        && MathF.Abs(a.Y - b.Y) <= Vec3Tolerance
        && MathF.Abs(a.Z - b.Z) <= Vec3Tolerance;

    private static float LerpAngleRaw(int rawA, int rawB, float w)
    {
        int a = rawA, b = rawB;
        int d = b - a;
        if (d < -0x8000) b += 0x10000;
        else if (d > 0x8000) a += 0x10000;
        return a + (b - a) * w;
    }

    private static bool WithinAngleTolerance(int actualRaw, float reference)
    {
        float diff = actualRaw - reference;
        diff -= MathF.Round(diff / 65536f) * 65536f;
        return MathF.Abs(diff) <= AngleToleranceRaw;
    }
}
