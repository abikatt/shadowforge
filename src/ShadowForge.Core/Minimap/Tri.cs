using System.Numerics;

namespace ShadowForge.Minimap;

/// <summary>
/// One triangle, positions only.
/// </summary>
public readonly record struct Tri(Vector3 A, Vector3 B, Vector3 C)
{
    public Vector3 Min => new(
        MathF.Min(A.X, MathF.Min(B.X, C.X)),
        MathF.Min(A.Y, MathF.Min(B.Y, C.Y)),
        MathF.Min(A.Z, MathF.Min(B.Z, C.Z)));

    public Vector3 Max => new(
        MathF.Max(A.X, MathF.Max(B.X, C.X)),
        MathF.Max(A.Y, MathF.Max(B.Y, C.Y)),
        MathF.Max(A.Z, MathF.Max(B.Z, C.Z)));
}
