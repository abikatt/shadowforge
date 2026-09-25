using System.Numerics;
using ShadowForge.Minimap;

namespace ShadowForge.Tests.Minimap;

public sealed class StageMeshTests
{
    [Fact]
    public void ToPreviewMesh_KeepsRenderTrianglesAndIgnoresCollision()
    {
        var stage = new StageMesh
        {
            Render =
            [
                new Tri(new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(0, 0, 10)),
                new Tri(new Vector3(10, 0, 0), new Vector3(10, 0, 10), new Vector3(0, 0, 10)),
            ],
            Collision = [new Tri(new Vector3(-500, 0, 0), new Vector3(500, 0, 0), new Vector3(0, 0, 500))],
        };

        var mesh = stage.ToPreviewMesh();

        Assert.Equal(2, mesh.TriangleCount);
        Assert.Equal(new Vector3(5, 0, 5), mesh.Center);
    }
}
