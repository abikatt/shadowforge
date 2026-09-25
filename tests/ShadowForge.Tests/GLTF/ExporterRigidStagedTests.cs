using System.Numerics;
using System.Text.Json.Nodes;
using ShadowForge.Formats.GLTF;
using ShadowForge.Formats.HDB;

namespace ShadowForge.Tests.GLTF;

/// <summary>
/// Staged groups on rigid (map) models are texture blends. They export to a second
/// "_staged" mesh with TEXCOORD_0/1/2 under the stage-0 material, and the mesh's sfLayers
/// extra names the stage-0/1/2 textures. Unstaged groups keep a single UV set.
/// </summary>
public sealed class ExporterRigidStagedTests
{
    /// <summary>
    /// Textures "base", "layer" and "mul", one VA of three vertices with distinct UV sets,
    /// and two groups over it: unstaged (mat 0) and staged (mat 0, stage1 = 1, stage2 = 2).
    /// </summary>
    private static ModelFile BuildLayeredModel()
    {
        var raw = new byte[3 * 100];
        ExportFixtures.EncodeSkinnedVertex(raw, 0, new Vector3(0, 0, 0), new Vector2(0.25f, 0.5f),
            new Vector2(0.125f, 0.375f), new Vector2(0.0625f, 0.75f));
        ExportFixtures.EncodeSkinnedVertex(raw, 100, new Vector3(1, 0, 0), new Vector2(0.5f, 0.5f),
            new Vector2(0.25f, 0.375f), new Vector2(0.125f, 0.75f));
        ExportFixtures.EncodeSkinnedVertex(raw, 200, new Vector3(0, 1, 0), new Vector2(0.25f, 0.25f),
            new Vector2(0.125f, 0.5f), new Vector2(0.0625f, 0.5f));

        return new ModelFile
        {
            Textures =
            {
                new TextureEntry { Name = "base" },
                new TextureEntry { Name = "layer" },
                new TextureEntry { Name = "mul" },
            },
            TextureCount = 3,
            VertexArrays =
            {
                new VertexArray { VAType = 0, VertexCount = 3, RawVertices = raw },
            },
            IndexArrays =
            {
                new IndexArray { Indices = new ushort[] { 0, 1, 2 } },
                new IndexArray { Indices = new ushort[] { 0, 1, 2 },
                                 Stage1TexIndex = 1, Stage2TexIndex = 2 },
            },
            MeshGroups =
            {
                new MeshGroup { VAIndex = 0, IAIndex = 0, MaterialIndex = 0, Topology = 0 },
                new MeshGroup { VAIndex = 0, IAIndex = 1, MaterialIndex = 0, Topology = 0,
                                Stage1TexIndex = 1, Stage2TexIndex = 2 },
            },
        };
    }

    private static SharpGLTF.Schema2.ModelRoot ExportManyAndLoad(ModelFile model)
    {
        string outPath = Path.Combine(Path.GetTempPath(),
            "sf_rigid_staged_" + Guid.NewGuid().ToString("N") + ".glb");
        try
        {
            SceneExporter.ExportMany([new RigidPlacement("stage_a", model, "AREA_0/PRI_0")], outPath);
            return SharpGLTF.Schema2.ModelRoot.Load(outPath);
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Fact]
    public void ExportMany_StagedDraw_GoesToStagedMeshWithThreeUVSets()
    {
        var gltf = ExportManyAndLoad(BuildLayeredModel());

        var plain = Assert.Single(gltf.LogicalMeshes, m => m.Name == "stage_a_0");
        var staged = Assert.Single(gltf.LogicalMeshes, m => m.Name == "stage_a_0_staged");
        Assert.Null(Assert.Single(plain.Primitives).GetVertexAccessor("TEXCOORD_1"));
        var prim = Assert.Single(staged.Primitives);
        Assert.NotNull(prim.GetVertexAccessor("TEXCOORD_0"));
        Assert.NotNull(prim.GetVertexAccessor("TEXCOORD_1"));
        Assert.NotNull(prim.GetVertexAccessor("TEXCOORD_2"));
        Assert.Equal("base", prim.Material.Name);
        Assert.Contains("stage_a_0_staged", gltf.LogicalNodes.Select(n => n.Name));
    }

    [Fact]
    public void ExportMany_StagedMesh_NamesLayerTexturesInExtras()
    {
        var gltf = ExportManyAndLoad(BuildLayeredModel());

        var staged = Assert.Single(gltf.LogicalMeshes, m => m.Name == "stage_a_0_staged");
        var extras = Assert.IsType<JsonObject>(staged.Extras);
        var layer = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(extras["sfLayers"])));
        Assert.Equal("base", (string?)layer["stage0"]);
        Assert.Equal("layer", (string?)layer["stage1"]);
        Assert.Equal("mul", (string?)layer["stage2"]);
    }
}
