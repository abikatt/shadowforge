using System.Numerics;
using ShadowForge.Formats.GLTF;
using ShadowForge.Formats.HDB;

namespace ShadowForge.Tests.GLTF;

/// <summary>
/// Staged groups (Stage1TexIndex >= 0) export as primitives of a mesh with TEXCOORD_0/1/2,
/// under a material named after the stage-1 texture with sfStage0Texture, sfStage2Texture
/// and sfStage2TexCoord extras. Unstaged groups keep a single UV set.
/// </summary>
public sealed class ExporterEyeTests
{
    /// <summary>
    /// One identity bone, textures "body" and "eye_r", one skinned VA of three vertices
    /// with distinct UV0/UV1/UV2, and two triangle-list groups over it: unstaged (mat 0)
    /// and staged (mat 0, stage1 = 1, stage2 = -1).
    /// </summary>
    private static ModelFile BuildStagedModel()
    {
        var raw = new byte[3 * 100];
        ExportFixtures.EncodeSkinnedVertex(raw, 0, new Vector3(0, 0, 0), new Vector2(0.25f, 0.5f),
            new Vector2(0.125f, 0.375f), new Vector2(0.0625f, 0.75f));
        ExportFixtures.EncodeSkinnedVertex(raw, 100, new Vector3(1, 0, 0), new Vector2(0.5f, 0.5f),
            new Vector2(0.25f, 0.375f), new Vector2(0.125f, 0.75f));
        ExportFixtures.EncodeSkinnedVertex(raw, 200, new Vector3(0, 1, 0), new Vector2(0.25f, 0.25f),
            new Vector2(0.125f, 0.5f), new Vector2(0.0625f, 0.5f));

        var model = new ModelFile
        {
            Bones = { new Bone { Index = 0, Name = "root" } },
            Textures =
            {
                new TextureEntry { Name = "body" },
                new TextureEntry { Name = "eye_r" },
            },
            TextureCount = 2,
            VertexArrays =
            {
                new VertexArray { VAType = 0, VertexCount = 3, RawVertices = raw },
            },
            IndexArrays =
            {
                new IndexArray { Indices = new ushort[] { 0, 1, 2 } },
                new IndexArray { Indices = new ushort[] { 0, 1, 2 },
                                 Stage1TexIndex = 1, Stage2TexIndex = -1 },
            },
            MeshGroups =
            {
                new MeshGroup { VAIndex = 0, IAIndex = 0, MaterialIndex = 0,
                                Topology = 0, BonePalette = { 0 } },
                new MeshGroup { VAIndex = 0, IAIndex = 1, MaterialIndex = 0,
                                Topology = 0, BonePalette = { 0 },
                                Stage1TexIndex = 1, Stage2TexIndex = -1 },
            },
        };
        return model;
    }

    private static SharpGLTF.Schema2.ModelRoot ExportAndLoad(ModelFile model)
    {
        string outPath = Path.Combine(Path.GetTempPath(),
            "sf_eye_" + Guid.NewGuid().ToString("N") + ".glb");
        try
        {
            SceneExporter.Export(model, outPath);
            return SharpGLTF.Schema2.ModelRoot.Load(outPath);
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Fact]
    public void StagedPrimitive_HasThreeTexCoordAccessors()
    {
        var gltf = ExportAndLoad(BuildStagedModel());

        var prims = gltf.LogicalMeshes.SelectMany(m => m.Primitives).ToList();
        var staged = prims.Where(p => p.GetVertexAccessor("TEXCOORD_2") != null).ToList();
        Assert.Single(staged);
        Assert.NotNull(staged[0].GetVertexAccessor("TEXCOORD_0"));
        Assert.NotNull(staged[0].GetVertexAccessor("TEXCOORD_1"));

        var unstaged = prims.Where(p => p.GetVertexAccessor("TEXCOORD_2") == null).ToList();
        Assert.Single(unstaged);
        Assert.NotNull(unstaged[0].GetVertexAccessor("TEXCOORD_0"));
        Assert.Null(unstaged[0].GetVertexAccessor("TEXCOORD_1"));
    }

    [Fact]
    public void StagedPrimitive_TexCoord1And2_CarryEyeUVsUnmangled()
    {
        var gltf = ExportAndLoad(BuildStagedModel());

        var staged = gltf.LogicalMeshes.SelectMany(m => m.Primitives)
            .Single(p => p.GetVertexAccessor("TEXCOORD_2") != null);

        var uv1 = staged.GetVertexAccessor("TEXCOORD_1")!.AsVector2Array();
        var uv2 = staged.GetVertexAccessor("TEXCOORD_2")!.AsVector2Array();
        Assert.Contains(new Vector2(0.125f, 0.375f), uv1);
        Assert.Contains(new Vector2(0.25f, 0.375f), uv1);
        Assert.Contains(new Vector2(0.125f, 0.75f), uv2);
        Assert.Contains(new Vector2(0.0625f, 0.5f), uv2);
    }

    [Fact]
    public void StagedMaterial_NamedAfterStage1_WithStageExtras()
    {
        var gltf = ExportAndLoad(BuildStagedModel());

        var staged = gltf.LogicalMeshes.SelectMany(m => m.Primitives)
            .Single(p => p.GetVertexAccessor("TEXCOORD_2") != null);
        var mat = staged.Material;
        Assert.NotNull(mat);
        Assert.Equal("eye_r", mat!.Name);

        var extras = Assert.IsType<System.Text.Json.Nodes.JsonObject>(mat.Extras);
        Assert.True(extras.ContainsKey("sfStage0Texture"));
        Assert.Equal("body", (string?)extras["sfStage0Texture"]);
        Assert.True(extras.ContainsKey("sfStage2Texture"));
        Assert.Null(extras["sfStage2Texture"]);
        Assert.Equal(2, (int?)extras["sfStage2TexCoord"]);
    }

    [Fact]
    public void UnstagedMaterial_HasNoStageExtras()
    {
        var gltf = ExportAndLoad(BuildStagedModel());

        var unstaged = gltf.LogicalMeshes.SelectMany(m => m.Primitives)
            .Single(p => p.GetVertexAccessor("TEXCOORD_2") == null);
        var mat = unstaged.Material;
        Assert.NotNull(mat);
        Assert.Equal("body", mat!.Name);
        var extras = mat.Extras as System.Text.Json.Nodes.JsonObject;
        Assert.True(extras is null || !extras.ContainsKey("sfStage0Texture"));
    }

    [Fact]
    public void StagedRigidGroup_ExportsToStagedMesh()
    {
        var model = BuildStagedModel();
        model.Bones.Clear();

        var gltf = ExportAndLoad(model);

        var staged = Assert.Single(gltf.LogicalMeshes, m => m.Name == "mesh_0_staged");
        Assert.NotNull(Assert.Single(staged.Primitives).GetVertexAccessor("TEXCOORD_2"));
        Assert.Empty(gltf.LogicalSkins);
    }

    /// <summary>
    /// The pc05 layout: the face texture (index 1) is bound as stage 0 under staged draws
    /// and by no unstaged group. Textures are "body" (mat 0, unstaged), "face" (stage 0
    /// only) and "eye_r" (stage 1).
    /// </summary>
    private static ModelFile BuildFaceOnlyUnderStageModel()
    {
        var model = BuildStagedModel();
        model.Textures.Insert(1, new TextureEntry { Name = "face" });
        model.TextureCount = 3;
        model.IndexArrays[1].Stage1TexIndex = 2;
        model.MeshGroups[1].MaterialIndex = 1;
        model.MeshGroups[1].Stage1TexIndex = 2;
        return model;
    }

    [Fact]
    public void Stage0TextureUsedOnlyUnderStagedDraws_IsStillWrittenAsMaterialAndImage()
    {
        string dir = Path.Combine(Path.GetTempPath(), "sf_eye0_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            ExportFixtures.WriteSolidDDS(dir, "body", 200, 50, 50);
            ExportFixtures.WriteSolidDDS(dir, "face", 50, 200, 50);
            ExportFixtures.WriteSolidDDS(dir, "eye_r", 50, 50, 200);
            string glb = Path.Combine(dir, "model.glb");
            SceneExporter.Export(BuildFaceOnlyUnderStageModel(), glb, textureDir: dir);
            var gltf = SharpGLTF.Schema2.ModelRoot.Load(glb);

            var staged = gltf.LogicalMeshes.SelectMany(m => m.Primitives)
                .Single(p => p.GetVertexAccessor("TEXCOORD_2") != null);
            var extras = Assert.IsType<System.Text.Json.Nodes.JsonObject>(staged.Material!.Extras);
            Assert.Equal("face", (string?)extras["sfStage0Texture"]);

            var face = gltf.LogicalMaterials.SingleOrDefault(m => m.Name == "face");
            Assert.NotNull(face);
            var img = face!.FindChannel("BaseColor")?.Texture?.PrimaryImage;
            Assert.NotNull(img);
            Assert.Equal("face", img!.Name);
            Assert.Contains(gltf.LogicalImages, i => i.Name == "face");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Stage0TextureUsedOnlyUnderStagedDraws_WrittenOnceWhenSharedByTwoEyes()
    {
        var model = BuildFaceOnlyUnderStageModel();
        model.Textures.Add(new TextureEntry { Name = "eye_l" });
        model.TextureCount = 4;
        model.IndexArrays.Add(new IndexArray { Indices = new ushort[] { 0, 1, 2 },
                                               Stage1TexIndex = 3, Stage2TexIndex = -1 });
        model.MeshGroups.Add(new MeshGroup { VAIndex = 0, IAIndex = 2, MaterialIndex = 1,
                                             Topology = 0, BonePalette = { 0 },
                                             Stage1TexIndex = 3, Stage2TexIndex = -1 });
        var gltf = ExportAndLoad(model);
        Assert.Single(gltf.LogicalMaterials, m => m.Name == "face");
    }
}
