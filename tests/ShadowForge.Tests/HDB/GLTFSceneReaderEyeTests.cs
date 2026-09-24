using System.Numerics;
using Microsoft.Extensions.Logging;
using ShadowForge.Formats.GLTF;
using ShadowForge.Formats.HDB;
using ShadowForge.Formats.HDB.Import;
using ShadowForge.Tests.GLTF;
using ShadowForge.Tests.Logging;

namespace ShadowForge.Tests.HDB;

/// <summary>
/// The inverse of ExporterEyeTests: an exported staged material (named after its stage-1
/// texture, with sfStage0Texture, sfStage2Texture and sfStage2TexCoord extras and
/// TEXCOORD_0/1/2) reads back into ImportVertex.UVEye/UVEyelid and
/// ImportTriangle.Stage1Index/Stage2Index, with MaterialIndex naming the stage-0 texture.
/// </summary>
public sealed class GLTFSceneReaderEyeTests
{
    private static readonly Vector2[] UV0 =
        { new(0.25f, 0.5f), new(0.5f, 0.5f), new(0.25f, 0.25f) };
    private static readonly Vector2[] UVEye =
        { new(0.125f, 0.375f), new(0.25f, 0.375f), new(0.125f, 0.5f) };
    private static readonly Vector2[] UVEyelid =
        { new(0.0625f, 0.75f), new(0.125f, 0.75f), new(0.0625f, 0.5f) };

    /// <summary>
    /// One identity bone, three textures, one skinned VA of three vertices,
    /// an unstaged group (mat 0) and a staged group (mat 0, stage1 = 1,
    /// stage2 as given) over it.
    /// </summary>
    private static ModelFile BuildStagedModel(int stage2TexIndex, string[] textureNames)
    {
        var raw = new byte[3 * 100];
        for (int i = 0; i < 3; i++)
            ExportFixtures.EncodeSkinnedVertex(raw, i * 100, new Vector3(i, 0, 0), UV0[i], UVEye[i], UVEyelid[i]);

        var model = new ModelFile
        {
            Bones = { new Bone { Index = 0, Name = "root" } },
            TextureCount = textureNames.Length,
            VertexArrays =
            {
                new VertexArray { VAType = 0, VertexCount = 3, RawVertices = raw },
            },
            IndexArrays =
            {
                new IndexArray { Indices = new ushort[] { 0, 1, 2 } },
                new IndexArray { Indices = new ushort[] { 0, 1, 2 },
                                 Stage1TexIndex = 1, Stage2TexIndex = stage2TexIndex },
            },
            MeshGroups =
            {
                new MeshGroup { VAIndex = 0, IAIndex = 0, MaterialIndex = 0,
                                Topology = 0, BonePalette = { 0 } },
                new MeshGroup { VAIndex = 0, IAIndex = 1, MaterialIndex = 0,
                                Topology = 0, BonePalette = { 0 },
                                Stage1TexIndex = 1, Stage2TexIndex = stage2TexIndex },
            },
        };
        foreach (var n in textureNames)
            model.Textures.Add(new TextureEntry { Name = n });
        return model;
    }

    private static ImportTriangle StagedTri(ImportScene scene) =>
        scene.Triangles.Single(t => t.Stage1Index >= 0);

    private static ImportTriangle UnstagedTri(ImportScene scene) =>
        scene.Triangles.Single(t => t.Stage1Index < 0);

    [Fact]
    public void StagedGlb_ResolvesEyeTextureAndUVSets()
    {
        string dir = Path.Combine(Path.GetTempPath(), "sf_rdeye_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            ExportFixtures.WriteSolidDDS(dir, "body", 200, 50, 50);
            ExportFixtures.WriteSolidDDS(dir, "eye_r", 50, 50, 200);
            string glb = Path.Combine(dir, "model.glb");
            SceneExporter.Export(BuildStagedModel(-1, new[] { "body", "eye_r" }), glb, textureDir: dir);

            var log = new CapturingLogger();
            var scene = GLTFSceneReader.Read(glb, log, formatOverride: null);

            int eyeIdx = scene.Textures.FindIndex(t => t.Name == "eye_r" && !t.IsNormalMap);
            int bodyIdx = scene.Textures.FindIndex(t => t.Name == "body" && !t.IsNormalMap);
            Assert.True(eyeIdx >= 0, "eye_r texture registered");
            Assert.True(bodyIdx >= 0, "body texture registered");

            var staged = StagedTri(scene);
            Assert.Equal(eyeIdx, staged.Stage1Index);
            Assert.Equal(-1, staged.Stage2Index);

            Assert.Equal(bodyIdx, staged.MaterialIndex);

            var unstaged = UnstagedTri(scene);
            Assert.Equal(-1, unstaged.Stage1Index);
            Assert.Equal(-1, unstaged.Stage2Index);

            var stagedVerts = new[] { staged.A, staged.B, staged.C }
                .Select(i => scene.Vertices[i]).ToList();
            foreach (var expected in UVEye)
                Assert.Contains(stagedVerts, v => Approx(v.UVEye, expected));
            foreach (var expected in UVEyelid)
                Assert.Contains(stagedVerts, v => Approx(v.UVEyelid, expected));

            var unstagedVerts = new[] { unstaged.A, unstaged.B, unstaged.C }
                .Select(i => scene.Vertices[i]);
            Assert.All(unstagedVerts, v =>
            {
                Assert.Equal(Vector2.Zero, v.UVEye);
                Assert.Equal(Vector2.Zero, v.UVEyelid);
            });
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void StagedGlb_Stage2ResolvesViaDDSFallback()
    {
        string dir = Path.Combine(Path.GetTempPath(), "sf_rdeye_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            ExportFixtures.WriteSolidDDS(dir, "body", 200, 50, 50);
            ExportFixtures.WriteSolidDDS(dir, "eye_r", 50, 50, 200);

            ExportFixtures.WriteSolidDDS(dir, "lid_r", 50, 200, 50);
            string glb = Path.Combine(dir, "model.glb");
            SceneExporter.Export(BuildStagedModel(2, new[] { "body", "eye_r", "lid_r" }), glb, textureDir: dir);

            var log = new CapturingLogger();
            var scene = GLTFSceneReader.Read(glb, log, formatOverride: null);

            int lidIdx = scene.Textures.FindIndex(t => t.Name == "lid_r" && !t.IsNormalMap);
            Assert.True(lidIdx >= 0, "lid_r recovered via DDS fallback");
            Assert.Equal(lidIdx, StagedTri(scene).Stage2Index);
            Assert.DoesNotContain(log.Entries, e => e.Level == LogLevel.Warning);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void StagedGlb_UnresolvableStage2_KeptAsNameOnlyEntry()
    {
        string dir = Path.Combine(Path.GetTempPath(), "sf_rdeye_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            ExportFixtures.WriteSolidDDS(dir, "body", 200, 50, 50);
            ExportFixtures.WriteSolidDDS(dir, "eye_r", 50, 50, 200);

            string glb = Path.Combine(dir, "model.glb");
            SceneExporter.Export(BuildStagedModel(2, new[] { "body", "eye_r", "lid_r" }), glb, textureDir: dir);

            var log = new CapturingLogger();
            var scene = GLTFSceneReader.Read(glb, log, formatOverride: null);

            int stage2 = StagedTri(scene).Stage2Index;
            Assert.True(stage2 >= 0, "stage-2 binding kept by name, not dropped");
            var lidTex = scene.Textures[stage2];
            Assert.Equal("lid_r", lidTex.Name);
            Assert.Empty(lidTex.DDSBytes);

            Assert.Contains(log.Entries, e =>
                e.Level == LogLevel.Warning
                && e.Message.Contains("lid_r")
                && e.Message.Contains("lid_r.dds"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    private static bool Approx(Vector2 a, Vector2 b) =>
        MathF.Abs(a.X - b.X) < 1e-3f && MathF.Abs(a.Y - b.Y) < 1e-3f;

    [Fact]
    public void StagedGlb_Stage0UsedOnlyUnderStagedDraws_ResolvesToFaceTexture()
    {
        string dir = Path.Combine(Path.GetTempPath(), "sf_rdeye_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            ExportFixtures.WriteSolidDDS(dir, "body", 200, 50, 50);
            ExportFixtures.WriteSolidDDS(dir, "face", 50, 200, 50);
            ExportFixtures.WriteSolidDDS(dir, "eye_r", 50, 50, 200);
            var model = BuildStagedModel(-1, new[] { "body", "face", "eye_r" });
            model.IndexArrays[1].Stage1TexIndex = 2;
            model.MeshGroups[1].MaterialIndex = 1;
            model.MeshGroups[1].Stage1TexIndex = 2;
            string glb = Path.Combine(dir, "model.glb");
            SceneExporter.Export(model, glb, textureDir: dir);

            var log = new CapturingLogger();
            var scene = GLTFSceneReader.Read(glb, log, formatOverride: null);

            int faceIdx = scene.Textures.FindIndex(t => t.Name == "face" && !t.IsNormalMap);
            Assert.True(faceIdx >= 0, "face texture registered");
            Assert.Equal(faceIdx, StagedTri(scene).MaterialIndex);
            Assert.DoesNotContain(log.Entries, e => e.Message.Contains("sfStage0Texture"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
