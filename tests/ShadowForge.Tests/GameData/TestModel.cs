using System.Numerics;
using ShadowForge.Formats.HDB.Import;

namespace ShadowForge.Tests.GameData;

internal static class TestModel
{
    public static byte[] WithTextures(params string[] textureNames)
    {
        var scene = new ImportScene
        {
            Skinned = false,
            Bones = new List<ImportBone> { new() { Index = 0, Name = "root", ParentIndex = -1 } },
            Textures = textureNames.Select(n => new ImportTexture { Name = n }).ToList(),
            Vertices = new List<ImportVertex>
            {
                new() { WorldPosition = Vector3.Zero, WorldNormal = Vector3.UnitY, UV = Vector2.Zero },
                new() { WorldPosition = Vector3.One, WorldNormal = Vector3.UnitY, UV = Vector2.One },
                new() { WorldPosition = new Vector3(1, 0, 1), WorldNormal = Vector3.UnitY, UV = new Vector2(1, 0) },
            },
            Triangles = new List<ImportTriangle> { new() { A = 0, B = 1, C = 2, MaterialIndex = 0 } },
        };
        return FileBuilder.Build(scene, BatchBuilder.Build(scene));
    }
}
