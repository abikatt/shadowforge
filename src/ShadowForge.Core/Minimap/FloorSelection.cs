using System.Numerics;

namespace ShadowForge.Minimap;

/// <summary>
/// Picks the floor-facing triangles of each source and intersects the two
/// rasterized floors.
/// </summary>
internal static class FloorSelection
{
    private const float FloorNormalY = 0.7f;
    private const float FloorHeightCut = 0.55f;
    private const float HeightAgreement = 40f;
    private const float MinAgreementFrac = 0.15f;

    /// <summary>
    /// Floor-facing render triangles. Without a collision mesh to agree with,
    /// <paramref name="applyHeightCut"/> also drops triangles in the top part
    /// of the stage's height range, which are roofs and ceilings.
    /// </summary>
    public static List<Tri> Render(IReadOnlyList<Tri> tris, bool applyHeightCut)
    {
        if (tris.Count == 0) return new List<Tri>();

        float minY = float.MaxValue;
        float maxY = float.MinValue;
        foreach (var t in tris)
        {
            minY = MathF.Min(minY, t.Min.Y);
            maxY = MathF.Max(maxY, t.Max.Y);
        }

        float heightCutY = applyHeightCut ? minY + FloorHeightCut * (maxY - minY) : float.PositiveInfinity;

        var floor = new List<Tri>();
        foreach (var t in tris)
        {
            if (!IsFloorFacing(t)) continue;

            float centroidY = (t.A.Y + t.B.Y + t.C.Y) / 3f;
            if (centroidY > heightCutY) continue;

            floor.Add(t);
        }

        return floor;
    }

    public static List<Tri> Collision(IReadOnlyList<Tri> tris) => tris.Where(IsFloorFacing).ToList();

    /// <summary>
    /// Collision floor that is also drawn within <see cref="HeightAgreement"/>
    /// world units. Falls back to the whole collision floor when less than
    /// <see cref="MinAgreementFrac"/> of it agrees.
    /// </summary>
    public static FloorLayer Agreeing(FloorLayer solid, FloorLayer drawn)
    {
        var covered = new bool[solid.Covered.Length];
        int solidCount = 0, agreeCount = 0;
        for (int i = 0; i < covered.Length; i++)
        {
            if (!solid.Covered[i]) continue;
            solidCount++;
            if (!drawn.Covered[i]) continue;
            if (MathF.Abs(solid.Height[i] - drawn.Height[i]) > HeightAgreement) continue;
            covered[i] = true;
            agreeCount++;
        }

        if (solidCount > 0 && agreeCount < MinAgreementFrac * solidCount)
            return solid;

        return new FloorLayer(covered, solid.Height);
    }

    private static bool IsFloorFacing(Tri t)
    {
        Vector3 normal = Vector3.Cross(t.B - t.A, t.C - t.A);
        float lenSq = normal.LengthSquared();
        if (lenSq < 1e-12f) return false;
        return normal.Y / MathF.Sqrt(lenSq) > FloorNormalY;
    }
}
