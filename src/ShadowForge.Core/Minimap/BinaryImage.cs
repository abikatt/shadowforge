namespace ShadowForge.Minimap;

/// <summary>
/// Row-major boolean pixel masks.
/// </summary>
internal static class BinaryImage
{
    public static bool[] Threshold(float[] coverage)
    {
        var inside = new bool[coverage.Length];
        for (int i = 0; i < coverage.Length; i++)
            inside[i] = coverage[i] >= 0.5f;
        return inside;
    }

    public static bool[] Invert(bool[] src)
    {
        var dst = new bool[src.Length];
        for (int i = 0; i < src.Length; i++)
            dst[i] = !src[i];
        return dst;
    }

    /// <summary>
    /// Morphological close with a square window of <paramref name="radius"/>.
    /// </summary>
    public static bool[] Close(bool[] src, int width, int height, int radius)
        => Sweep(Sweep(src, width, height, radius, erode: false), width, height, radius, erode: true);

    /// <summary>
    /// Dilate sets a pixel when any window pixel is set, ignoring pixels past
    /// the edge. Erode clears a pixel when any window pixel is clear, counting
    /// pixels past the edge as clear.
    /// </summary>
    private static bool[] Sweep(bool[] src, int width, int height, int radius, bool erode)
    {
        bool seek = !erode;
        var dst = new bool[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (src[y * width + x] == seek) { dst[y * width + x] = seek; continue; }

                bool found = false;
                for (int dy = -radius; dy <= radius && !found; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= height) { found = erode; continue; }
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int nx = x + dx;
                        if (nx < 0 || nx >= width)
                        {
                            if (erode) { found = true; break; }
                            continue;
                        }
                        if (src[ny * width + nx] == seek) { found = true; break; }
                    }
                }
                dst[y * width + x] = found ? seek : !seek;
            }
        }
        return dst;
    }
}
