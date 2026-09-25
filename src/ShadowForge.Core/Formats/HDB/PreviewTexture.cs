using ShadowForge.Formats.DDS;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ShadowForge.Formats.HDB;

/// <summary>
/// A decoded texture for the preview, scaled down so its longer side is at most the limit
/// given to <see cref="Decode"/>. Sampling uses mirrored repeat, because Blue Dragon UVs
/// span [-1, +1] on mirror-symmetric parts.
/// </summary>
public sealed class PreviewTexture
{
    public int Width { get; }
    public int Height { get; }
    internal Rgba32[] Pixels { get; }

    internal PreviewTexture(int width, int height, Rgba32[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    /// <summary>
    /// Decodes a .dds, or the first depth slice of a .36t volume when
    /// <paramref name="isVolume"/> is set.
    /// </summary>
    public static PreviewTexture Decode(byte[] raw, bool isVolume = false, int maxSize = 512)
    {
        var (rgba, width, height) = Converter.DecodeRgba(raw, isVolume);
        using var image = Image.LoadPixelData<Rgba32>(rgba, width, height);
        if (Math.Max(width, height) > maxSize)
        {
            float scale = (float)maxSize / Math.Max(width, height);
            image.Mutate(x => x.Resize(Math.Max(1, (int)(width * scale)), Math.Max(1, (int)(height * scale))));
        }
        var pixels = new Rgba32[image.Width * image.Height];
        image.CopyPixelDataTo(pixels);
        return new PreviewTexture(image.Width, image.Height, pixels);
    }

    /// <summary>
    /// Nearest texel at a glTF-style UV, where (0, 0) is the top-left of the image.
    /// </summary>
    internal Rgba32 Sample(float u, float v)
    {
        int x = (int)(Mirror(u) * Width);
        int y = (int)(Mirror(v) * Height);
        return Pixels[Math.Min(y, Height - 1) * Width + Math.Min(x, Width - 1)];
    }

    private static float Mirror(float t)
    {
        float m = t - 2f * MathF.Floor(t * 0.5f);
        return m > 1f ? 2f - m : m;
    }
}
