using System.Numerics;
using ShadowForge.Formats.HOC;
using ShadowForge.IO;

namespace ShadowForge.Tests.Formats.HOC;

public sealed class CollisionMeshTests
{

    private static byte[] BuildFile()
    {
        const int surfaceOffset = 0x30;
        const int triangleOffset = surfaceOffset + CollisionMesh.SurfaceStride;
        int size = triangleOffset + CollisionMesh.TriangleStride;

        var data = new byte[size];
        BigEndian.WriteUInt32(data, 0x00, CollisionMesh.Magic);
        BigEndian.WriteUInt32(data, 0x04, 0x00010000);
        BigEndian.WriteInt32(data, 0x08, size);
        BigEndian.WriteInt32(data, 0x0C, 2);

        BigEndian.WriteInt32(data, 0x10, surfaceOffset - 0x10);
        BigEndian.WriteInt32(data, 0x14, CollisionMesh.SurfaceStride);
        BigEndian.WriteUInt32(data, 0x18, 0x203);

        BigEndian.WriteInt32(data, 0x20, triangleOffset - 0x20);
        BigEndian.WriteInt32(data, 0x24, CollisionMesh.TriangleStride);
        BigEndian.WriteUInt32(data, 0x28, 0x200);

        BigEndian.WriteUInt32(data, surfaceOffset, 0x1E);
        BigEndian.WriteUInt32(data, surfaceOffset + 4, 2);
        BigEndian.WriteUInt32(data, surfaceOffset + 8, 0xDD00FF00);

        BigEndian.WriteInt32(data, triangleOffset, surfaceOffset - triangleOffset);
        WriteVector3(data, triangleOffset + 0x08, new Vector3(-1f, 2f, -3f));
        WriteVector3(data, triangleOffset + 0x14, new Vector3(4f, 2f, -3f));
        WriteVector3(data, triangleOffset + 0x20, new Vector3(-1f, 2f, 5f));
        WriteVector3(data, triangleOffset + 0x2C, new Vector3(0f, 1f, 0f));
        return data;
    }

    private static void WriteVector3(byte[] data, int offset, Vector3 v)
    {
        BigEndian.WriteFloat(data, offset, v.X);
        BigEndian.WriteFloat(data, offset + 4, v.Y);
        BigEndian.WriteFloat(data, offset + 8, v.Z);
    }

    [Fact]
    public void Read_ParsesTrianglesAndSurfaces()
    {
        var hoc = CollisionMesh.Read(BuildFile());

        var surface = Assert.Single(hoc.Surfaces);
        Assert.Equal(0x1Eu, surface.Material);
        Assert.Equal(2u, surface.Flags);
        Assert.Equal(0xDD00FF00u, surface.DebugColor);

        var tri = Assert.Single(hoc.Triangles);
        Assert.Equal(new Vector3(-1f, 2f, -3f), tri.A);
        Assert.Equal(new Vector3(4f, 2f, -3f), tri.B);
        Assert.Equal(new Vector3(-1f, 2f, 5f), tri.C);
        Assert.Equal(new Vector3(0f, 1f, 0f), tri.Normal);
        Assert.Equal(0, tri.Surface);
    }

    [Fact]
    public void Read_RejectsForeignMagic()
    {
        var data = BuildFile();
        BigEndian.WriteUInt32(data, 0, 0x4844425F);
        Assert.Throws<InvalidDataException>(() => CollisionMesh.Read(data));
    }

    [Fact]
    public void Read_RejectsChunkReachingPastTheFile()
    {
        var data = BuildFile();
        BigEndian.WriteInt32(data, 0x24, data.Length * 4);
        Assert.Throws<InvalidDataException>(() => CollisionMesh.Read(data));
    }

    [SkippableFact]
    public void ShippedCollisionNormalsAgreeWithWinding()
    {
        var hoc = CollisionMesh.Read(RetailData.Read(@"map\town\bg08\bg08_01c.hocb"));
        Assert.NotEmpty(hoc.Triangles);
        Assert.NotEmpty(hoc.Surfaces);
        Assert.All(hoc.Triangles, t => Assert.InRange(t.Surface, 0, hoc.Surfaces.Count - 1));

        int agree = 0, tested = 0;
        foreach (var t in hoc.Triangles)
        {
            Vector3 wound = Vector3.Cross(t.B - t.A, t.C - t.A);
            if (wound.LengthSquared() < 1e-6f || t.Normal.LengthSquared() < 1e-6f) continue;
            tested++;
            if (Vector3.Dot(Vector3.Normalize(wound), Vector3.Normalize(t.Normal)) > 0.9f) agree++;
        }

        Assert.True(tested > 0);
        Assert.True(agree > 0.99f * tested, $"{agree}/{tested} normals agree with winding");
    }
}
