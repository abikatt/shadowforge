using ShadowForge.Minimap;

namespace ShadowForge.Tests.Minimap;

public sealed class MaskTests
{
    static Tri Quad0(float x0, float z0, float x1, float z1, float y) =>
        new(new(x0, y, z0), new(x1, y, z1), new(x1, y, z0));
    static Tri Quad1(float x0, float z0, float x1, float z1, float y) =>
        new(new(x0, y, z0), new(x0, y, z1), new(x1, y, z1));

    static float CoverageAt(MaskResult m, float wx, float wz)
    {
        int px = (int)((wx - m.WorldMinX) / m.WorldScaleX * m.Width);
        int pz = (int)((wz - m.WorldMinZ) / m.WorldScaleY * m.Height);
        return m.Mask[pz * m.Width + px];
    }

    [Fact]
    public void FlatFloorQuadFillsItsRect()
    {
        var tris = new List<Tri> {
            Quad0(0, 0, 100, 100, 0), Quad1(0, 0, 100, 100, 0),
            new(new(0, 0, 0), new(0, 10, 0), new(0, 10, 100)),
        };
        var m = Mask.Rasterize(tris);
        Assert.Equal(512, m.Width);
        Assert.Equal(512, m.Height);
        Assert.True(CoverageAt(m, 50, 50) > 0.9f);
        Assert.True(CoverageAt(m, -1.5f, 50) < 0.1f);
    }

    [Fact]
    public void CeilingAboveHeightCutIsExcluded()
    {
        var tris = new List<Tri> {
            Quad0(0, 0, 100, 100, 0), Quad1(0, 0, 100, 100, 0),
            Quad0(40, 40, 60, 60, 100), Quad1(40, 40, 60, 60, 100),
        };
        var m = Mask.Rasterize(tris);
        Assert.True(CoverageAt(m, 50, 50) > 0.9f);
    }

    [Fact]
    public void WideExtentPicksHalfHeightTexture()
    {
        var tris = new List<Tri> { Quad0(0, 0, 500, 100, 0), Quad1(0, 0, 500, 100, 0) };
        var m = Mask.Rasterize(tris);
        Assert.Equal(512, m.Width);
        Assert.Equal(256, m.Height);
    }

    [Fact]
    public void TinySpecksAreDropped()
    {
        var tris = new List<Tri> {
            Quad0(0, 0, 100, 100, 0), Quad1(0, 0, 100, 100, 0),
            Quad0(100.5f, 100.5f, 100.9f, 100.9f, 0), Quad1(100.5f, 100.5f, 100.9f, 100.9f, 0),
        };
        var m = Mask.Rasterize(tris);
        Assert.True(CoverageAt(m, 100.7f, 100.7f) < 0.1f);
    }

    [Fact]
    public void DistantSpeckDoesNotBalloonTheWorldRect()
    {
        var tris = new List<Tri> {
            Quad0(0, 0, 100, 100, 0), Quad1(0, 0, 100, 100, 0),
            Quad0(1000, 1000, 1003, 1003, 0), Quad1(1000, 1000, 1003, 1003, 0),
        };
        var m = Mask.Rasterize(tris);

        Assert.InRange(m.WorldScaleX, 100f, 130f);
        Assert.InRange(m.WorldScaleY, 100f, 130f);
        Assert.True(CoverageAt(m, 50, 50) > 0.9f);
    }

    [Fact]
    public void RaisedFloorAgreeingWithCollisionIsKept()
    {
        var render = new List<Tri> {
            Quad0(0, 0, 100, 100, 0), Quad1(0, 0, 100, 100, 0),
            Quad0(200, 0, 300, 100, 200), Quad1(200, 0, 300, 100, 200),
            Quad0(70, 70, 82, 82, 100), Quad1(70, 70, 82, 82, 100),
            new(new(0, 0, 0), new(0, 300, 0), new(0, 300, 100)),
        };
        var collision = new List<Tri> {
            Quad0(0, 0, 100, 100, 0), Quad1(0, 0, 100, 100, 0),
            Quad0(200, 0, 300, 100, 200), Quad1(200, 0, 300, 100, 200),
            Quad0(70, 70, 82, 82, 100), Quad1(70, 70, 82, 82, 100),
        };
        var m = Mask.Rasterize(render, collision);
        Assert.True(CoverageAt(m, 250, 50) > 0.9f);
        Assert.True(CoverageAt(m, 50, 50) > 0.9f);
        Assert.True(CoverageAt(m, 76, 76) < 0.1f);
    }
}
