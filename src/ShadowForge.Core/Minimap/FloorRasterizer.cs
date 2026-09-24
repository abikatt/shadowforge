using System.Numerics;

namespace ShadowForge.Minimap;

/// <summary>
/// Rasterizes floor triangles at a supersampled resolution and box-filters
/// the result down to texture pixels.
/// </summary>
internal static class FloorRasterizer
{
    /// <summary>
    /// Where triangles overlap, a pixel keeps the height closest to
    /// <paramref name="nearTo"/> when given, else the highest.
    /// </summary>
    public static FloorLayer Rasterize(List<Tri> floorTris, WorldRect rect, int ssWidth, int ssHeight,
        float[]? nearTo = null)
    {
        var bits = new bool[ssWidth * ssHeight];
        var height = new float[ssWidth * ssHeight];
        Array.Fill(height, float.NegativeInfinity);
        if (floorTris.Count == 0 || rect.ScaleX <= 0f || rect.ScaleZ <= 0f)
            return new FloorLayer(bits, height);

        foreach (var t in floorTris)
        {
            Vector2 a = rect.ToPixel(t.A, ssWidth, ssHeight);
            Vector2 b = rect.ToPixel(t.B, ssWidth, ssHeight);
            Vector2 c = rect.ToPixel(t.C, ssWidth, ssHeight);

            int minPx = Math.Max(0, (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
            int maxPx = Math.Min(ssWidth - 1, (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
            int minPz = Math.Max(0, (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
            int maxPz = Math.Min(ssHeight - 1, (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));

            float area = Cross(b - a, c - a);
            bool degenerate = MathF.Abs(area) < 1e-6f;

            for (int pz = minPz; pz <= maxPz; pz++)
            {
                for (int px = minPx; px <= maxPx; px++)
                {
                    var p = new Vector2(px + 0.5f, pz + 0.5f);
                    if (!PointInTriangle(p, a, b, c)) continue;

                    int idx = pz * ssWidth + px;
                    bits[idx] = true;

                    float y = degenerate ? t.Max.Y : InterpolateY(p, a, b, c, t, area);
                    if (KeepHeight(height[idx], y, nearTo?[idx]))
                        height[idx] = y;
                }
            }
        }

        return new FloorLayer(bits, height);
    }

    public static float[] DownsampleCoverage(bool[] ssBits, int ssWidth, int width, int height, int supersample)
    {
        var coverage = new float[width * height];
        float norm = 1f / (supersample * supersample);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int count = 0;
                int baseX = x * supersample;
                int baseY = y * supersample;
                for (int sy = 0; sy < supersample; sy++)
                {
                    int row = (baseY + sy) * ssWidth;
                    for (int sx = 0; sx < supersample; sx++)
                    {
                        if (ssBits[row + baseX + sx]) count++;
                    }
                }
                coverage[y * width + x] = count * norm;
            }
        }

        return coverage;
    }

    /// <summary>
    /// Highest height per pixel, NaN where no sample was drawn.
    /// </summary>
    public static float[] DownsampleHeight(float[] ssHeights, int ssWidth, int width, int height, int supersample)
    {
        var top = new float[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float best = float.NegativeInfinity;
                int baseX = x * supersample;
                int baseY = y * supersample;
                for (int sy = 0; sy < supersample; sy++)
                {
                    int row = (baseY + sy) * ssWidth;
                    for (int sx = 0; sx < supersample; sx++)
                    {
                        float v = ssHeights[row + baseX + sx];
                        if (v > best) best = v;
                    }
                }
                top[y * width + x] = float.IsNegativeInfinity(best) ? float.NaN : best;
            }
        }
        return top;
    }

    private static bool KeepHeight(float held, float candidate, float? target)
    {
        if (float.IsNegativeInfinity(held)) return true;
        if (target is not { } t || float.IsNegativeInfinity(t)) return candidate > held;
        return MathF.Abs(candidate - t) < MathF.Abs(held - t);
    }

    private static float InterpolateY(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Tri t, float area)
    {
        float wa = Cross(b - p, c - p) / area;
        float wb = Cross(c - p, a - p) / area;
        float wc = 1f - wa - wb;
        return wa * t.A.Y + wb * t.B.Y + wc * t.C.Y;
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p - a, b - a);
        float d2 = Cross(p - b, c - b);
        float d3 = Cross(p - c, a - c);

        bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNeg && hasPos);
    }

    private static float Cross(Vector2 lhs, Vector2 rhs) => lhs.X * rhs.Y - lhs.Y * rhs.X;
}
