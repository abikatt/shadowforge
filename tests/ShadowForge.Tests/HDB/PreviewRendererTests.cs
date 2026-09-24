using ShadowForge.Formats.HDB;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShadowForge.Tests.HDB;

public sealed class PreviewRendererTests
{
    [Fact]
    public void Render_HumanoidFixture_ProducesValidPng()
    {
        var raw = ModelReader.Read(File.ReadAllBytes(TestFile.HDB));
        var model = ModelCooker.Bake(raw);

        byte[] png = PreviewRenderer.Render(model, width: 128, height: 128);

        Assert.True(png.Length > 8, "PNG output unexpectedly tiny");

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
            png.AsSpan(0, 8).ToArray());
    }

    [Fact]
    public void Render_HumanoidFixture_DrawsModelPixelsOverBackground()
    {
        var raw = ModelReader.Read(File.ReadAllBytes(TestFile.HDB));
        var model = ModelCooker.Bake(raw);

        byte[] png = PreviewRenderer.Render(model, width: 128, height: 128);

        using var image = Image.Load<Rgba32>(png);
        Assert.Equal(128, image.Width);
        Assert.Equal(128, image.Height);

        var background = new Rgba32(0x2A, 0x2A, 0x2C, 0xFF);
        int drawn = 0;
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                foreach (var px in row)
                    if (!px.Equals(background)) drawn++;
            }
        });

        int total = image.Width * image.Height;
        Assert.True(drawn > total / 10, $"Only {drawn}/{total} pixels were drawn; rasterizer likely missed the model");
    }

    [Fact]
    public void Render_EmptyModel_ProducesBackgroundOnlyPng()
    {
        var model = new ModelFile();

        byte[] png = PreviewRenderer.Render(model, width: 32, height: 32);

        using var image = Image.Load<Rgba32>(png);
        var background = new Rgba32(0x2A, 0x2A, 0x2C, 0xFF);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                foreach (var px in row)
                    Assert.Equal(background, px);
            }
        });
    }
}
