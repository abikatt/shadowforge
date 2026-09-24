using System.Numerics;

namespace ShadowForge.Formats.HDB.Import;

/// <summary>
/// Bone matrix math matching the runtime: World = ParentWorld * T * R(euler) * S, with
/// R = Rz(z) * Ry(y) * Rx(x). Decoding retail skeletons with this convention poses them
/// correctly. Vertices are encoded against the same composition as the skeleton.
/// </summary>
public static class RuntimeMath
{
    /// <summary>
    /// Rz * Ry * Rx as a row-major System.Numerics matrix chain.
    /// System.Numerics uses row-vector convention (v * M), so the
    /// column-vector product Rz*Ry*Rx becomes CreateRotationX *
    /// CreateRotationY * CreateRotationZ in Matrix4x4 multiplication order.
    /// </summary>
    public static Matrix4x4 EulerZyxToMatrix(Vector3 euler)
        => Matrix4x4.CreateRotationX(euler.X)
         * Matrix4x4.CreateRotationY(euler.Y)
         * Matrix4x4.CreateRotationZ(euler.Z);

    /// <summary>
    /// Extracts euler angles such that Rz(z)*Ry(y)*Rx(x) equals the rotation
    /// of <paramref name="q"/> (column-vector convention).
    /// </summary>
    public static Vector3 QuaternionToEulerZyx(Quaternion q)
    {
        q = Quaternion.Normalize(q);

        float m00 = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
        float m10 = 2 * (q.X * q.Y + q.W * q.Z);
        float m20 = 2 * (q.X * q.Z - q.W * q.Y);
        float m21 = 2 * (q.Y * q.Z + q.W * q.X);
        float m22 = 1 - 2 * (q.X * q.X + q.Y * q.Y);
        float m11 = 1 - 2 * (q.X * q.X + q.Z * q.Z);
        float m12 = 2 * (q.Y * q.Z - q.W * q.X);

        float sy = -m20;
        float cy = MathF.Sqrt(MathF.Max(0f, 1f - sy * sy));
        if (cy > 1e-6f)
        {
            return new Vector3(
                MathF.Atan2(m21, m22),
                MathF.Atan2(sy, cy),
                MathF.Atan2(m10, m00));
        }
        return new Vector3(
            MathF.Atan2(-m12, m11),
            sy >= 0 ? MathF.PI / 2f : -MathF.PI / 2f,
            0f);
    }

    /// <summary>
    /// Local transform exactly as the runtime composes it from the disk fields
    /// (row-vector order: S, then R, then T).
    /// </summary>
    public static Matrix4x4 ComposeLocal(Vector3 translation, Vector3 euler, Vector3 scale)
        => Matrix4x4.CreateScale(scale)
         * EulerZyxToMatrix(euler)
         * Matrix4x4.CreateTranslation(translation);

    /// <summary>
    /// Fills World / InverseWorld on every bone by composing the
    /// runtime-encoded TRS down the parent chain.
    /// </summary>
    public static void ComputeWorldTransforms(List<ImportBone> bones)
    {
        var done = new bool[bones.Count];

        void Resolve(int i)
        {
            if (done[i]) return;
            var b = bones[i];
            var local = ComposeLocal(b.Translation, QuaternionToEulerZyx(b.Rotation), b.Scale);
            if (b.ParentIndex >= 0)
            {
                Resolve(b.ParentIndex);
                b.World = local * bones[b.ParentIndex].World;
            }
            else
            {
                b.World = local;
            }
            if (!Matrix4x4.Invert(b.World, out var inv)) inv = Matrix4x4.Identity;
            b.InverseWorld = inv;
            done[i] = true;
        }

        for (int i = 0; i < bones.Count; i++) Resolve(i);
    }

    public static Vector3 TransformPoint(Matrix4x4 m, Vector3 p) => Vector3.Transform(p, m);

    public static Vector3 TransformNormal(Matrix4x4 m, Vector3 n)
    {
        var r = Vector3.TransformNormal(n, m);
        float len = r.Length();
        return len > 1e-9f ? r / len : n;
    }
}
