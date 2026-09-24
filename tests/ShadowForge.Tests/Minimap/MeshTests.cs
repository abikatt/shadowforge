using System.Numerics;
using ShadowForge.Formats.HDB;
using ShadowForge.Minimap;

namespace ShadowForge.Tests.Minimap;

public sealed class MeshTests
{
    [SkippableFact]
    public void CollectsWorldSpaceTrianglesFromMapHDB()
    {
        var model = ModelCooker.Bake(ModelReader.Read(RetailData.Read(@"map\dungeon\dg05\dg05_03a.hdb")));
        var tris = new List<Tri>();
        Mesh.CollectTriangles(model, Matrix4x4.Identity, tris);

        Assert.True(tris.Count > 10_000, $"got {tris.Count}");

        float minX = tris.Min(t => t.Min.X);
        float maxX = tris.Max(t => t.Max.X);
        Assert.InRange(minX, -60f, 100f);
        Assert.True(maxX - minX > 100f);
    }

    [Fact]
    public void EmptyModelYieldsNoTriangles()
    {
        var tris = new List<Tri>();
        Mesh.CollectTriangles(new ModelFile(), Matrix4x4.CreateTranslation(5, 0, 0), tris);
        Assert.Empty(tris);
    }
}
