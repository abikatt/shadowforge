using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ShadowForge.Formats.DDS;
using ShadowForge.Minimap;

namespace ShadowForge.Tests.Minimap;

public sealed class GoldenTests
{
    [SkippableFact]
    public void Dg0503MaskMatchesShippedMap()
    {
        byte[] shippedDDS = RetailData.Read(@"minimap\MM_dg05_03.dds");

        var mesh = MapAssembler.AssembleStage(RetailData.Files, "dg05_03");
        var mask = Mask.Rasterize(mesh.Render, mesh.Collision);
        Assert.Equal(512, mask.Width);
        Assert.Equal(256, mask.Height);

        Assert.InRange(-mask.WorldMinX, 40f, 100f);
        Assert.InRange(mask.WorldScaleX, 730f, 820f);

        using var shipped = Image.Load<Rgba32>(Converter.ConvertToPngBytes(shippedDDS));
        int inter = 0, union = 0;
        for (int z = 0; z < 256; z++)
        for (int x = 0; x < 512; x++)
        {
            float wx = (x + 0.5f) / 512f * 768f - 68f;
            float wz = (z + 0.5f) / 256f * 384f - 192f;
            int mx = (int)((wx - mask.WorldMinX) / mask.WorldScaleX * mask.Width);
            int mz = (int)((wz - mask.WorldMinZ) / mask.WorldScaleY * mask.Height);
            bool mine = mx >= 0 && mz >= 0 && mx < mask.Width && mz < mask.Height
                        && mask.Mask[mz * mask.Width + mx] > 0.5f;
            var p = shipped[x, z];
            bool theirs = Math.Abs(p.R - 0x20) > 24 || Math.Abs(p.G - 0x36) > 24 || Math.Abs(p.B - 0x41) > 24;
            if (mine && theirs) inter++;
            if (mine || theirs) union++;
        }
        Assert.True(union > 0 && (float)inter / union > 0.75f,
                    $"IoU {(float)inter / union:F2}");
    }
}
