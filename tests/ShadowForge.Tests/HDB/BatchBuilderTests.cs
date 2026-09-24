using System.Numerics;
using ShadowForge.Formats.HDB.Import;

namespace ShadowForge.Tests.HDB;

/// <summary>
/// BatchBuilder groups triangles by (MaterialIndex, Stage1Index, Stage2Index), so
/// different stage bindings get separate batches even under the same material.
/// </summary>
public sealed class BatchBuilderTests
{
    [Fact]
    public void Build_TrianglesWithSameMaterialButDifferentStages_CreatesSeparateBatches()
    {
        var scene = new ImportScene
        {
            Skinned = false,
            Vertices = new List<ImportVertex>
            {
                new() { WorldPosition = Vector3.Zero, WorldNormal = Vector3.UnitY, UV = Vector2.Zero },
                new() { WorldPosition = Vector3.One, WorldNormal = Vector3.UnitY, UV = Vector2.One },
                new() { WorldPosition = new Vector3(1, 0, 1), WorldNormal = Vector3.UnitY, UV = new Vector2(1, 0) },
                new() { WorldPosition = new Vector3(0, 0, 1), WorldNormal = Vector3.UnitY, UV = new Vector2(0, 1) },
            },
            Triangles = new List<ImportTriangle>
            {
                new() { A = 0, B = 1, C = 2, MaterialIndex = 0, Stage1Index = -1, Stage2Index = -1 },
                new() { A = 1, B = 3, C = 2, MaterialIndex = 0, Stage1Index = 1, Stage2Index = 2 },
            },
        };

        var batches = BatchBuilder.Build(scene);

        Assert.Equal(2, batches.Count);
        Assert.Equal(0, batches[0].MaterialIndex);
        Assert.Equal(-1, batches[0].Stage1Index);
        Assert.Equal(-1, batches[0].Stage2Index);

        Assert.Equal(0, batches[1].MaterialIndex);
        Assert.Equal(1, batches[1].Stage1Index);
        Assert.Equal(2, batches[1].Stage2Index);
    }
}
