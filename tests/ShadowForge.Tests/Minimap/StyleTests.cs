using SixLabors.ImageSharp.PixelFormats;
using ShadowForge.Minimap;

namespace ShadowForge.Tests.Minimap;

public sealed class StyleTests
{
    [Fact]
    public void RendersBackgroundFillAndOutline()
    {
        var mask = new float[64 * 64];
        for (int z = 20; z < 44; z++)
            for (int x = 20; x < 44; x++)
                mask[z * 64 + x] = 1f;
        var elevation = new float[64 * 64];
        Array.Fill(elevation, float.NaN);
        var m = new MaskResult { Mask = mask, Elevation = elevation, Width = 64, Height = 64,
                                 WorldMinX = 0, WorldMinZ = 0, WorldScaleX = 64, WorldScaleY = 64 };
        using var img = Style.Render(m);

        Rgba32 bg = img[2, 2], center = img[32, 32], edge = img[19, 32];
        Assert.Equal(255, bg.A);
        Assert.Equal(new Rgba32(0x20, 0x36, 0x41), bg);

        Assert.True(center.B > bg.B && center.R < 0x90);

        Assert.True(edge.R > 0xC0 && edge.G > 0xC0 && edge.B > 0xC0);

        Assert.Equal(center, img[20, 32]);
    }
}
