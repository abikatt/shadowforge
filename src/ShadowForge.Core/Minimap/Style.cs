using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShadowForge.Minimap;

/// <summary>
/// Renders a <see cref="MaskResult"/> coverage mask into the opaque
/// background/fill/outline RGBA look sampled from the shipped minimap textures
/// (e.g. MM_dg01_01, MM_dg05_03), shading the fill by elevation and drawing a
/// contour where the floor steps between terraces.
/// </summary>
public static class Style
{
    private static readonly Rgba32 Background = new(0x20, 0x36, 0x41);
    private static readonly Rgba32 FillBase = new(0x3E, 0x69, 0x80);
    private static readonly Rgba32 FillLow = new(0x33, 0x57, 0x6C);
    private static readonly Rgba32 FillHigh = new(0x78, 0xA2, 0xBC);
    private static readonly Rgba32 Outline = new(0xEE, 0xFA, 0xFF);

    private const float OutlineWidthPx = 2f;
    private const float OutlineSoftBandPx = 1.5f;

    private const float AACoverageLow = 0.05f;
    private const float AACoverageHigh = 0.95f;

    private const int TerraceCount = 5;

    private const float MinTerraceSpanWorld = 60f;

    private const float ContourStrength = 0.8f;

    public static Image<Rgba32> Render(MaskResult mask)
    {
        int width = mask.Width;
        int height = mask.Height;

        bool[] inside = BinaryImage.Threshold(mask.Mask);

        float[] dOut = DistanceField.Chamfer(inside, width, height);

        int[] terrace = Terraces(mask, inside, out int terraceCount);
        bool[] contour = terraceCount > 1 ? Contours(terrace, inside, width, height) : new bool[inside.Length];

        var image = new Image<Rgba32>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                float coverage = mask.Mask[idx];
                Rgba32 insideColor = InsideColor(terrace[idx], terraceCount);
                if (contour[idx])
                    insideColor = Lerp(insideColor, Outline, ContourStrength);
                Rgba32 outsideColor = OutsideColor(dOut[idx]);

                Rgba32 color;
                if (coverage > AACoverageLow && coverage < AACoverageHigh)
                    color = Lerp(outsideColor, insideColor, coverage);
                else
                    color = coverage >= 0.5f ? insideColor : outsideColor;

                image[x, y] = color;
            }
        }

        return image;
    }

    private static int[] Terraces(MaskResult mask, bool[] inside, out int count)
    {
        var terrace = new int[mask.Mask.Length];
        count = 1;

        var heights = new List<float>();
        for (int i = 0; i < inside.Length; i++)
        {
            if (inside[i] && !float.IsNaN(mask.Elevation[i]))
                heights.Add(mask.Elevation[i]);
        }
        if (heights.Count == 0) return terrace;

        heights.Sort();
        float low = heights[(int)(heights.Count * 0.02f)];
        float high = heights[Math.Min(heights.Count - 1, (int)(heights.Count * 0.98f))];
        if (high - low < MinTerraceSpanWorld) return terrace;

        count = TerraceCount;
        float step = (high - low) / count;
        for (int i = 0; i < terrace.Length; i++)
        {
            float h = mask.Elevation[i];
            if (float.IsNaN(h)) continue;
            terrace[i] = Math.Clamp((int)((h - low) / step), 0, count - 1);
        }
        return terrace;
    }

    private static bool[] Contours(int[] terrace, bool[] inside, int width, int height)
    {
        var contour = new bool[terrace.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                if (!inside[idx]) continue;

                int here = terrace[idx];
                bool step = (x + 1 < width && inside[idx + 1] && terrace[idx + 1] > here)
                         || (y + 1 < height && inside[idx + width] && terrace[idx + width] > here);
                contour[idx] = step;
            }
        }
        return contour;
    }

    private static Rgba32 InsideColor(int terrace, int terraceCount) =>
        terraceCount > 1
            ? Lerp(FillLow, FillHigh, terrace / (float)(terraceCount - 1))
            : FillBase;

    private static Rgba32 OutsideColor(float distanceToInside)
    {
        if (distanceToInside <= OutlineWidthPx)
            return Outline;

        if (distanceToInside <= OutlineWidthPx + OutlineSoftBandPx)
        {
            float t = (distanceToInside - OutlineWidthPx) / OutlineSoftBandPx;
            return Lerp(Outline, Background, t);
        }

        return Background;
    }

    private static Rgba32 Lerp(Rgba32 a, Rgba32 b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        byte r = (byte)(a.R + (b.R - a.R) * t);
        byte g = (byte)(a.G + (b.G - a.G) * t);
        byte bch = (byte)(a.B + (b.B - a.B) * t);
        return new Rgba32(r, g, bch, 255);
    }
}
