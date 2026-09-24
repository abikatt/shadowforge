using System.Numerics;

namespace ShadowForge.Formats.HOC;

/// <summary>
/// One collision triangle: stage-space corners plus its authored normal. Surface indexes
/// <see cref="CollisionMesh.Surfaces"/> and is -1 when the triangle's pointer selects none.
/// </summary>
public readonly record struct Triangle(Vector3 A, Vector3 B, Vector3 C, Vector3 Normal, int Surface);
