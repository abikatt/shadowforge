namespace ShadowForge.Minimap;

/// <summary>
/// Cleanup passes run on the downsampled coverage mask.
/// </summary>
internal static class MaskCleanup
{
    private const int CloseRadius = 2;
    private const float MinComponentFrac = 0.005f;
    private const float MaxHoleFrac = 0.004f;
    private const float StepTolerance = 12f;
    private const float MinRegionFrac = 0.02f;
    private const float SmoothSigma = 1.2f;

    /// <summary>
    /// Closes gaps, fills enclosed holes, and drops small or detached
    /// regions. Kept pixels take the larger of their coverage and the
    /// closed mask.
    /// </summary>
    public static float[] Filter(float[] coverage, float[] elevation, int width, int height)
    {
        bool[] closed = BinaryImage.Close(BinaryImage.Threshold(coverage), width, height, CloseRadius);
        FillSmallHoles(closed, width, height);
        DropDetachedRegions(closed, elevation, width, height);

        var components = new ComponentLabels(closed, width, height);
        bool[] keep = components.KeepAtLeast(MinComponentFrac);

        var result = new float[width * height];
        for (int i = 0; i < result.Length; i++)
        {
            if (keep[components.Labels[i]])
                result[i] = MathF.Max(coverage[i], closed[i] ? 1f : 0f);
        }

        return result;
    }

    /// <summary>
    /// Replaces the mask with a blurred signed distance field so edges come
    /// out antialiased instead of stair-stepped.
    /// </summary>
    public static float[] Smooth(float[] coverage, int width, int height)
    {
        bool[] inside = BinaryImage.Threshold(coverage);
        bool[] outside = BinaryImage.Invert(inside);

        float[] dIn = DistanceField.Chamfer(outside, width, height);
        float[] dOut = DistanceField.Chamfer(inside, width, height);

        var signed = new float[coverage.Length];
        for (int i = 0; i < signed.Length; i++)
            signed[i] = inside[i] ? dIn[i] : -dOut[i];

        float[] blurred = GaussianBlur(signed, width, height, SmoothSigma);

        var result = new float[coverage.Length];
        for (int i = 0; i < result.Length; i++)
            result[i] = Math.Clamp(0.5f + blurred[i], 0f, 1f);

        return result;
    }

    /// <summary>
    /// Gives covered pixels with NaN elevation the elevation of a neighbor,
    /// growing outward until nothing more can be filled.
    /// </summary>
    public static void FillElevationGaps(float[] mask, float[] elevation, int width, int height)
    {
        var missing = new List<int>();
        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i] > 0f && float.IsNaN(elevation[i]))
                missing.Add(i);
        }

        while (missing.Count > 0)
        {
            var stillMissing = new List<int>(missing.Count);
            foreach (int idx in missing)
            {
                int x = idx % width;
                int y = idx / width;
                float found = float.NaN;

                if (x > 0 && !float.IsNaN(elevation[idx - 1])) found = elevation[idx - 1];
                else if (x < width - 1 && !float.IsNaN(elevation[idx + 1])) found = elevation[idx + 1];
                else if (y > 0 && !float.IsNaN(elevation[idx - width])) found = elevation[idx - width];
                else if (y < height - 1 && !float.IsNaN(elevation[idx + width])) found = elevation[idx + width];

                if (float.IsNaN(found)) stillMissing.Add(idx);
                else elevation[idx] = found;
            }

            if (stillMissing.Count == missing.Count) break;
            missing = stillMissing;
        }
    }

    /// <summary>
    /// Clears regions that are small next to the largest one, where a region
    /// only spans neighbors whose floor heights differ by at most
    /// <see cref="StepTolerance"/>. Splits off ledges and rooftops that touch
    /// the main floor in plan view but sit at another height.
    /// </summary>
    private static void DropDetachedRegions(bool[] mask, float[] elevation, int width, int height)
    {
        var filledMask = new float[mask.Length];
        var filledElevation = new float[mask.Length];
        for (int i = 0; i < mask.Length; i++)
        {
            filledMask[i] = mask[i] ? 1f : 0f;
            filledElevation[i] = mask[i] ? elevation[i] : float.NaN;
        }
        FillElevationGaps(filledMask, filledElevation, width, height);

        var regions = new ComponentLabels(mask, width, height, (from, to) =>
        {
            float a = filledElevation[from], b = filledElevation[to];
            return float.IsNaN(a) || float.IsNaN(b) || MathF.Abs(b - a) <= StepTolerance;
        });
        bool[] keep = regions.KeepAtLeast(MinRegionFrac);

        for (int i = 0; i < mask.Length; i++)
        {
            if (regions.Labels[i] > 0 && !keep[regions.Labels[i]])
                mask[i] = false;
        }
    }

    /// <summary>
    /// Fills enclosed gaps no larger than <see cref="MaxHoleFrac"/> of the
    /// covered area. Gaps touching the texture edge are never filled.
    /// </summary>
    private static void FillSmallHoles(bool[] mask, int width, int height)
    {
        int filled = mask.Count(v => v);
        if (filled == 0) return;

        var gaps = new ComponentLabels(BinaryImage.Invert(mask), width, height);
        int[] labels = gaps.Labels;

        var openToEdge = new bool[gaps.Sizes.Count + 1];
        for (int x = 0; x < width; x++)
        {
            openToEdge[labels[x]] = true;
            openToEdge[labels[(height - 1) * width + x]] = true;
        }
        for (int y = 0; y < height; y++)
        {
            openToEdge[labels[y * width]] = true;
            openToEdge[labels[y * width + width - 1]] = true;
        }

        float cap = MaxHoleFrac * filled;
        for (int i = 0; i < mask.Length; i++)
        {
            int label = labels[i];
            if (label > 0 && !openToEdge[label] && gaps.Sizes[label - 1] <= cap)
                mask[i] = true;
        }
    }

    private static float[] GaussianBlur(float[] src, int width, int height, float sigma)
    {
        int radius = Math.Max(1, (int)MathF.Ceiling(sigma * 3f));
        var kernel = new float[radius * 2 + 1];
        float sum = 0f;
        for (int i = -radius; i <= radius; i++)
        {
            float v = MathF.Exp(-(i * i) / (2f * sigma * sigma));
            kernel[i + radius] = v;
            sum += v;
        }
        for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;

        float[] rows = Convolve(src, width, height, kernel, horizontal: true);
        return Convolve(rows, width, height, kernel, horizontal: false);
    }

    private static float[] Convolve(float[] src, int width, int height, float[] kernel, bool horizontal)
    {
        int radius = kernel.Length / 2;
        var dst = new float[src.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float acc = 0f;
                for (int k = -radius; k <= radius; k++)
                {
                    int idx = horizontal
                        ? y * width + Math.Clamp(x + k, 0, width - 1)
                        : Math.Clamp(y + k, 0, height - 1) * width + x;
                    acc += src[idx] * kernel[k + radius];
                }
                dst[y * width + x] = acc;
            }
        }
        return dst;
    }
}
