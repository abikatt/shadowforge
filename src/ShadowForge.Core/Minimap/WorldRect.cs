using System.Numerics;

namespace ShadowForge.Minimap;

/// <summary>
/// The world XZ rect a minimap texture covers and the texture's size: 512x512,
/// or 512x256 for stages at least <see cref="WideAspect"/> times wider than
/// deep.
/// </summary>
internal readonly record struct WorldRect(float MinX, float MinZ, float ScaleX, float ScaleZ, int Width, int Height)
{
    private const float MarginFrac = 0.02f;
    private const float WideAspect = 1.6f;
    private const int TextureWidth = 512;
    private const int TextureHeightTall = 512;
    private const int TextureHeightWide = 256;

    public static WorldRect Around(List<Tri> tris)
    {
        float minX = 0f, maxX = 0f, minZ = 0f, maxZ = 0f;
        if (tris.Count > 0)
        {
            minX = float.MaxValue; maxX = float.MinValue;
            minZ = float.MaxValue; maxZ = float.MinValue;
            foreach (var t in tris)
            {
                Vector3 min = t.Min, max = t.Max;
                minX = MathF.Min(minX, min.X);
                maxX = MathF.Max(maxX, max.X);
                minZ = MathF.Min(minZ, min.Z);
                maxZ = MathF.Max(maxZ, max.Z);
            }
        }

        return Fit(minX, maxX, minZ, maxZ);
    }

    /// <summary>
    /// A rect fitted to the covered pixels of <paramref name="mask"/>, or null
    /// when nothing is covered. Stray triangles far from the level widen the
    /// first rect, and the mask cleanup drops them.
    /// </summary>
    public WorldRect? CropTo(float[] mask)
    {
        int minPx = int.MaxValue, maxPx = int.MinValue, minPz = int.MaxValue, maxPz = int.MinValue;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (mask[y * Width + x] <= 0f) continue;
                if (x < minPx) minPx = x;
                if (x > maxPx) maxPx = x;
                if (y < minPz) minPz = y;
                if (y > maxPz) maxPz = y;
            }
        }
        if (maxPx < minPx) return null;

        float minX = MinX + minPx / (float)Width * ScaleX;
        float maxX = MinX + (maxPx + 1) / (float)Width * ScaleX;
        float minZ = MinZ + minPz / (float)Height * ScaleZ;
        float maxZ = MinZ + (maxPz + 1) / (float)Height * ScaleZ;

        return Fit(minX, maxX, minZ, maxZ);
    }

    public Vector2 ToPixel(Vector3 world, int width, int height)
    {
        float u = (world.X - MinX) / ScaleX;
        float v = (world.Z - MinZ) / ScaleZ;
        return new Vector2(u * width, v * height);
    }

    /// <summary>
    /// Pads the bounds by a margin, then grows the short axis so the rect's
    /// aspect matches the texture's.
    /// </summary>
    private static WorldRect Fit(float minX, float maxX, float minZ, float maxZ)
    {
        float extentX = maxX - minX;
        float extentZ = maxZ - minZ;
        float margin = MarginFrac * MathF.Max(extentX, extentZ);

        minX -= margin; maxX += margin;
        minZ -= margin; maxZ += margin;
        extentX = maxX - minX;
        extentZ = maxZ - minZ;

        bool wide = extentZ > 0f && extentX / extentZ >= WideAspect;
        int width = TextureWidth;
        int height = wide ? TextureHeightWide : TextureHeightTall;
        float targetAspect = width / (float)height;

        float currentAspect = extentZ > 0f ? extentX / extentZ : targetAspect;
        if (currentAspect < targetAspect)
        {
            float wantExtentX = extentZ * targetAspect;
            float grow = (wantExtentX - extentX) / 2f;
            minX -= grow; maxX += grow;
            extentX = maxX - minX;
        }
        else if (currentAspect > targetAspect)
        {
            float wantExtentZ = extentX / targetAspect;
            float grow = (wantExtentZ - extentZ) / 2f;
            minZ -= grow; maxZ += grow;
            extentZ = maxZ - minZ;
        }

        return new WorldRect(minX, minZ, extentX, extentZ, width, height);
    }
}
