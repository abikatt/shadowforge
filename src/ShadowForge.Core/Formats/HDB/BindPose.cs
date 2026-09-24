using System.Numerics;

namespace ShadowForge.Formats.HDB;

/// <summary>
/// World-space transform of one bone as a 3x3 matrix and a position. Matrices are
/// row-major with column vectors: world = Matrix * local + Position.
/// </summary>
public sealed record BoneGlobal(float[,] Matrix, Vector3 Position);

public static class BindPose
{
    /// <summary>
    /// Composes each bone's world transform onto its parent's. A bone whose ParentIndex is
    /// negative or out of range keeps its local transform. Bones caught in a parent cycle
    /// are composed in list order after everything else.
    /// </summary>
    public static BoneGlobal[] ComputeGlobals(IReadOnlyList<Bone> bones)
    {
        var globals = new BoneGlobal[bones.Count];

        var resolved = new bool[bones.Count];
        bool madeProgress = true;
        while (madeProgress)
        {
            madeProgress = false;
            for (int i = 0; i < bones.Count; i++)
            {
                if (resolved[i]) continue;
                var bone = bones[i];
                bool parentReady = bone.ParentIndex < 0
                                || bone.ParentIndex >= bones.Count
                                || globals[bone.ParentIndex] != null;
                if (!parentReady) continue;
                globals[i] = ResolveBoneGlobal(bone, bones, globals);
                resolved[i] = true;
                madeProgress = true;
            }
        }

        for (int i = 0; i < bones.Count; i++)
        {
            if (resolved[i]) continue;
            globals[i] = ResolveBoneGlobal(bones[i], bones, globals);
        }

        return globals;
    }

    /// <summary>
    /// Local rotation of a bone: ExtraEuler A * Euler * ExtraEuler B, or Euler alone when
    /// every ExtraEuler component is zero.
    /// </summary>
    public static float[,] LocalRotation(Bone bone)
    {
        var rotation = EulerToMatrix3(bone.EulerX, bone.EulerY, bone.EulerZ);
        if (!bone.ExtraEuler.Any(v => v != 0f))
            return rotation;

        var a = EulerToMatrix3(bone.ExtraEuler[0], bone.ExtraEuler[1], bone.ExtraEuler[2]);
        var b = EulerToMatrix3(bone.ExtraEuler[3], bone.ExtraEuler[4], bone.ExtraEuler[5]);
        return Mat3Mul(Mat3Mul(a, rotation), b);
    }

    private static BoneGlobal ResolveBoneGlobal(Bone bone, IReadOnlyList<Bone> bones, BoneGlobal[] globals)
    {
        var localPos = new Vector3(bone.PosX, bone.PosY, bone.PosZ);
        var scaleMatrix = new float[3, 3]
        {
            { bone.ScaleX, 0, 0 },
            { 0, bone.ScaleY, 0 },
            { 0, 0, bone.ScaleZ },
        };
        var localRotScale = Mat3Mul(LocalRotation(bone), scaleMatrix);

        if (bone.ParentIndex < 0 || bone.ParentIndex >= bones.Count || globals[bone.ParentIndex] == null)
            return new BoneGlobal(localRotScale, localPos);

        var parent = globals[bone.ParentIndex];
        return new BoneGlobal(
            Mat3Mul(parent.Matrix, localRotScale),
            Mat3Vec(parent.Matrix, localPos) + parent.Position);
    }

    /// <summary>
    /// World position and normal of a vertex, taken from its first influence. A vertex
    /// with no influences, or whose first influence names no known bone, keeps its stored
    /// position and normal.
    /// </summary>
    public static (Vector3 worldPos, Vector3 worldNormal) Anchor(
        Vertex v, IReadOnlyList<ushort> palette, BoneGlobal[] boneGlobals)
    {
        var normal = new Vector3(v.NormalX / 32767f, v.NormalY / 32767f, v.NormalZ / 32767f);
        if (v.Influences.Count == 0)
            return (new Vector3(v.PosX, v.PosY, v.PosZ), SafeNormalize(normal));

        var inf0 = v.Influences[0];
        var localPos = new Vector3(inf0.PosX, inf0.PosY, inf0.PosZ);
        int palIdx = inf0.PaletteIndex;
        int boneIdx = palIdx >= 0 && palIdx < palette.Count ? palette[palIdx] : 0;
        if (boneIdx < 0 || boneIdx >= boneGlobals.Length || boneGlobals[boneIdx] == null)
            return (localPos, SafeNormalize(normal));

        var bg = boneGlobals[boneIdx];
        return (Mat3Vec(bg.Matrix, localPos) + bg.Position, SafeNormalize(Mat3Vec(bg.Matrix, normal)));
    }

    /// <summary>
    /// Rz * Ry * Rx for column vectors.
    /// </summary>
    public static float[,] EulerToMatrix3(float x, float y, float z)
    {
        float cx = MathF.Cos(x), sx = MathF.Sin(x);
        float cy = MathF.Cos(y), sy = MathF.Sin(y);
        float cz = MathF.Cos(z), sz = MathF.Sin(z);

        return new float[3, 3]
        {
            { cy * cz, sx * sy * cz - cx * sz, cx * sy * cz + sx * sz },
            { cy * sz, sx * sy * sz + cx * cz, cx * sy * sz - sx * cz },
            { -sy,     sx * cy,                cx * cy                },
        };
    }

    public static float[,] Mat3Mul(float[,] a, float[,] b)
    {
        var r = new float[3, 3];
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                for (int k = 0; k < 3; k++)
                    r[i, j] += a[i, k] * b[k, j];
        return r;
    }

    public static Vector3 Mat3Vec(float[,] m, Vector3 v)
        => new(
            m[0, 0] * v.X + m[0, 1] * v.Y + m[0, 2] * v.Z,
            m[1, 0] * v.X + m[1, 1] * v.Y + m[1, 2] * v.Z,
            m[2, 0] * v.X + m[2, 1] * v.Y + m[2, 2] * v.Z);

    private static Vector3 SafeNormalize(Vector3 v)
    {
        var n = Vector3.Normalize(v);
        return float.IsNaN(n.X) ? Vector3.UnitY : n;
    }
}
