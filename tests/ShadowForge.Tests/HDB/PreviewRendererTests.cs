using System.Numerics;
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

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(90f, 0f)]
    [InlineData(200f, -60f)]
    [InlineData(45f, 89f)]
    public void RenderInto_AnyAngle_DrawsModelInsideFramingCircle(float yaw, float pitch)
    {
        var mesh = new PreviewMesh(TallBox(new Vector3(1f, 4f, 1f), offset: new Vector3(10f, 2f, -3f)));
        const int size = 96;
        var pixels = new Rgba32[size * size];
        var zBuffer = new float[size * size];

        PreviewRenderer.RenderInto(mesh, PreviewCamera.Default with { YawDeg = yaw, PitchDeg = pitch }, pixels, zBuffer, size, size);

        var background = new Rgba32(0x2A, 0x2A, 0x2C, 0xFF);
        int drawn = 0;
        float limit = 0.48f * size + 1.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            if (pixels[y * size + x].Equals(background)) continue;
            drawn++;
            float dx = x + 0.5f - size / 2f, dy = y + 0.5f - size / 2f;
            Assert.True(MathF.Sqrt(dx * dx + dy * dy) <= limit, $"pixel ({x},{y}) outside the framing circle");
        }
        Assert.True(drawn > 50, $"Only {drawn} pixels drawn at yaw {yaw}, pitch {pitch}");
    }

    [Fact]
    public void RenderInto_DefaultStructCamera_Throws()
    {
        var mesh = new PreviewMesh(TallBox(Vector3.One, Vector3.Zero));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PreviewRenderer.RenderInto(mesh, default, new Rgba32[16], new float[16], 4, 4));
    }

    [Fact]
    public void RenderInto_EmptyMesh_FillsBackground()
    {
        var mesh = PreviewRenderer.Prepare(new ModelFile());
        var pixels = new Rgba32[16];

        PreviewRenderer.RenderInto(mesh, PreviewCamera.Default, pixels, new float[16], 4, 4);

        Assert.All(pixels, px => Assert.Equal(new Rgba32(0x2A, 0x2A, 0x2C, 0xFF), px));
    }

    /// <summary>
    /// The 12 triangles of an axis-aligned box of the given size, its min corner at offset.
    /// </summary>
    private static List<PreviewRenderer.Triangle> TallBox(Vector3 size, Vector3 offset)
    {
        Vector3 C(int x, int y, int z) => offset + new Vector3(x * size.X, y * size.Y, z * size.Z);
        int[][] faces =
        [
            [0, 1, 3, 2], [4, 6, 7, 5], [0, 4, 5, 1], [2, 3, 7, 6], [0, 2, 6, 4], [1, 5, 7, 3],
        ];
        var corners = Enumerable.Range(0, 8).Select(i => C(i >> 2 & 1, i >> 1 & 1, i & 1)).ToArray();
        var tris = new List<PreviewRenderer.Triangle>();
        foreach (var f in faces)
        {
            tris.Add(new(corners[f[0]], corners[f[1]], corners[f[2]]));
            tris.Add(new(corners[f[0]], corners[f[2]], corners[f[3]]));
        }
        return tris;
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
