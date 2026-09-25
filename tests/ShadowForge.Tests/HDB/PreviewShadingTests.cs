using System.Numerics;
using ShadowForge.Formats.HDB;
using ShadowForge.Tests.GLTF;
using SixLabors.ImageSharp.PixelFormats;

namespace ShadowForge.Tests.HDB;

public sealed class PreviewShadingTests
{
    private const int Size = 64;
    private static readonly Rgba32 Background = new(0x2A, 0x2A, 0x2C, 0xFF);
    private static readonly PreviewCamera FrontOn = PreviewCamera.Default with { YawDeg = 0f, PitchDeg = 0f };

    /// <summary>
    /// Two triangles facing +Z side by side, the left one on texture slot 0 ("red") and the
    /// right one on slot 1 ("blue"), all UVs at the texture's centre.
    /// </summary>
    private static ModelFile TwoSlotModel()
    {
        var raw = new byte[6 * 100];
        Vector3[] corners =
        [
            new(0, 0, 0), new(1, 0, 0), new(0, 1, 0),
            new(2, 0, 0), new(3, 0, 0), new(2, 1, 0),
        ];
        for (int i = 0; i < corners.Length; i++)
            ExportFixtures.EncodeSkinnedVertex(raw, i * 100, corners[i], new Vector2(0.5f, 0.5f));

        return new ModelFile
        {
            Bones = { new Bone { Index = 0, Name = "root" } },
            Textures = { new TextureEntry { Name = "red" }, new TextureEntry { Name = "blue" } },
            TextureCount = 2,
            VertexArrays = { new VertexArray { VAType = 0, VertexCount = 6, RawVertices = raw } },
            IndexArrays =
            {
                new IndexArray { Indices = new ushort[] { 0, 1, 2 } },
                new IndexArray { Indices = new ushort[] { 3, 4, 5 } },
            },
            MeshGroups =
            {
                new MeshGroup { VAIndex = 0, IAIndex = 0, MaterialIndex = 0, Topology = 0, BonePalette = { 0 } },
                new MeshGroup { VAIndex = 0, IAIndex = 1, MaterialIndex = 1, Topology = 0, BonePalette = { 0 } },
            },
        };
    }

    private static Rgba32[] Render(PreviewMesh mesh, PreviewShading shading)
    {
        var pixels = new Rgba32[Size * Size];
        PreviewRenderer.RenderInto(mesh, FrontOn, shading, pixels, new float[Size * Size], Size, Size);
        return pixels;
    }

    private static HashSet<Rgba32> DrawnColors(Rgba32[] pixels) => pixels.Where(p => p != Background).ToHashSet();

    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "sf_preview_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Builder_SharesSlotsByTextureName_AndAppliesNameOverrides()
    {
        var model = TwoSlotModel();

        var mesh = new PreviewMeshBuilder()
            .Add(model, Matrix4x4.Identity)
            .Add(model, Matrix4x4.CreateTranslation(0, 5, 0))
            .Add(model, Matrix4x4.Identity, ["green"])
            .Build();

        Assert.Equal(["red", "blue", "green"], mesh.MaterialNames);
        Assert.Equal(6, mesh.TriangleCount);
        Assert.False(mesh.HasTextures);
    }

    [Fact]
    public void Flat_DrawsBothSlotsInOneGrey_MaterialColors_TellsThemApart()
    {
        var mesh = PreviewRenderer.Prepare(TwoSlotModel());

        Assert.Single(DrawnColors(Render(mesh, PreviewShading.Flat)));
        Assert.Equal(2, DrawnColors(Render(mesh, PreviewShading.MaterialColors)).Count);
    }

    [Fact]
    public void Wireframe_DrawsEdgesBrighterThanTheFill()
    {
        var colors = DrawnColors(Render(PreviewRenderer.Prepare(TwoSlotModel()), PreviewShading.Wireframe));

        Assert.Equal(2, colors.Count);
        var (dim, bright) = colors.OrderBy(c => c.R + c.G + c.B).ToArray() switch { var c => (c[0], c[1]) };
        Assert.True(bright.R > dim.R * 2, $"edge {bright} is not clearly brighter than fill {dim}");
    }

    [Fact]
    public void TexturedUnlit_SamplesEachSlotsTexture()
    {
        string dir = TempDir();
        try
        {
            ExportFixtures.WriteSolidDDS(dir, "red", 255, 0, 0);
            ExportFixtures.WriteSolidDDS(dir, "blue", 0, 0, 255);
            var mesh = PreviewRenderer.Prepare(TwoSlotModel()).WithTextures(dir);

            Assert.Equal(2, mesh.TexturesFound);
            var colors = DrawnColors(Render(mesh, PreviewShading.TexturedUnlit));
            Assert.Contains(colors, c => c.R > 200 && c.G < 40 && c.B < 40);
            Assert.Contains(colors, c => c.B > 200 && c.R < 40 && c.G < 40);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Textured_DrawsSlotWithMissingTextureInGrey()
    {
        string dir = TempDir();
        try
        {
            ExportFixtures.WriteSolidDDS(dir, "red", 255, 0, 0);
            var mesh = PreviewRenderer.Prepare(TwoSlotModel()).WithTextures(dir);

            Assert.True(mesh.HasTextures);
            Assert.Equal(1, mesh.TexturesFound);
            var colors = DrawnColors(Render(mesh, PreviewShading.Textured));
            Assert.Contains(colors, c => c.R > c.G * 3);
            Assert.Contains(colors, c => Math.Abs(c.R - c.G) < 12 && c.B >= c.G);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Smooth_WithoutNormals_FallsBackToFaceLighting()
    {
        var mesh = new PreviewMesh([new PreviewRenderer.Triangle(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0))]);

        Assert.Equal(DrawnColors(Render(mesh, PreviewShading.Flat)), DrawnColors(Render(mesh, PreviewShading.Smooth)));
    }

    [Theory]
    [InlineData(0.25f, 0)]
    [InlineData(0.75f, 1)]
    [InlineData(1.25f, 1)]
    [InlineData(-0.25f, 0)]
    [InlineData(1.75f, 0)]
    public void Sample_MirrorsOutsideZeroToOne(float u, int expectedTexel)
    {
        Rgba32[] texels = [new(255, 0, 0, 255), new(0, 0, 255, 255)];
        var texture = new PreviewTexture(2, 1, texels);

        Assert.Equal(texels[expectedTexel], texture.Sample(u, 0.5f));
    }
}
