namespace ShadowForge.Minimap;

/// <summary>
/// Distance to the nearest seed pixel, in pixels.
/// </summary>
public static class DistanceField
{
    private const float OrthoWeight = 3f;
    private const float DiagWeight = 4f;
    private const float Norm = 3f;

    /// <summary>
    /// 3/4-weighted two-pass chamfer distance to the nearest seed pixel.
    /// </summary>
    public static float[] Chamfer(bool[] seed, int width, int height)
    {
        float inf = (width + height) * DiagWeight;
        var d = new float[width * height];
        for (int i = 0; i < d.Length; i++)
            d[i] = seed[i] ? 0f : inf;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                float v = d[idx];
                if (x > 0) v = MathF.Min(v, d[idx - 1] + OrthoWeight);
                if (y > 0)
                {
                    int up = idx - width;
                    v = MathF.Min(v, d[up] + OrthoWeight);
                    if (x > 0) v = MathF.Min(v, d[up - 1] + DiagWeight);
                    if (x < width - 1) v = MathF.Min(v, d[up + 1] + DiagWeight);
                }
                d[idx] = v;
            }
        }

        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = width - 1; x >= 0; x--)
            {
                int idx = y * width + x;
                float v = d[idx];
                if (x < width - 1) v = MathF.Min(v, d[idx + 1] + OrthoWeight);
                if (y < height - 1)
                {
                    int down = idx + width;
                    v = MathF.Min(v, d[down] + OrthoWeight);
                    if (x < width - 1) v = MathF.Min(v, d[down + 1] + DiagWeight);
                    if (x > 0) v = MathF.Min(v, d[down - 1] + DiagWeight);
                }
                d[idx] = v;
            }
        }

        for (int i = 0; i < d.Length; i++)
            d[i] /= Norm;

        return d;
    }
}
