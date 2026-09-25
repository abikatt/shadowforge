using System.Numerics;
using System.Text.Json.Nodes;
using ShadowForge.Formats.HDB;
using ShadowForge.Formats.HMB;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;

namespace ShadowForge.Formats.GLTF;

using RigidMeshBuilder = MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>;
using RigidVertexBuilder = VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>;

using SkinnedMeshBuilder = MeshBuilder<VertexPositionNormal, VertexTexture1, VertexJoints4>;
using SkinnedVertexBuilder = VertexBuilder<VertexPositionNormal, VertexTexture1, VertexJoints4>;

using StagedMeshBuilder = MeshBuilder<VertexPositionNormal, VertexTexture3, VertexJoints4>;
using StagedVertexBuilder = VertexBuilder<VertexPositionNormal, VertexTexture3, VertexJoints4>;

using StagedRigidMeshBuilder = MeshBuilder<VertexPositionNormal, VertexTexture3, VertexEmpty>;
using StagedRigidVertexBuilder = VertexBuilder<VertexPositionNormal, VertexTexture3, VertexEmpty>;

/// <summary>
/// Writes cooked HDB models to glTF. Vertices are placed at their bind-pose world
/// position, and V is flipped back from the decoder's 1 - v.
/// </summary>
public static class SceneExporter
{
    public static IReadOnlyList<string> Export(ModelFile model, string outputPath,
        string? textureDir = null, bool embed = true, IReadOnlyList<MotionClip>? clips = null,
        IReadOnlyList<string>? textureNames = null)
    {
        var scene = new SceneBuilder();
        var warnings = new List<string>();

        if (textureNames is not null && textureNames.Count != model.TextureCount)
            warnings.Add($"texture override lists {textureNames.Count} names but the " +
                         $"model has {model.TextureCount} texture slots; mapping by ordinal");

        var boneGlobals = BindPose.ComputeGlobals(model.Bones);
        var boneNodes = SkeletonBuilder.Build(model, scene);
        var materials = SceneMaterials.Build(model, textureDir, textureNames);

        if (boneNodes.Length > 0)
        {
            BuildSkinnedMeshes(model, scene, materials, boneGlobals, boneNodes, textureDir);
            if (clips != null)
                warnings.AddRange(AnimationExporter.AddClips(boneNodes, model.Bones, clips));
        }
        else
        {
            BuildRigidMeshes(model, scene, materials, boneGlobals);
            if (clips != null && clips.Count > 0)
                warnings.Add($"model has no bones; skipping {clips.Count} animation clip(s)");
        }

        var gltf = scene.ToGltf2();
        SceneMaterials.AddStage0OnlyMaterials(model, materials, gltf);
        if (embed)
            gltf.SaveGLB(outputPath);
        else
            gltf.SaveGLTF(outputPath);

        return warnings;
    }

    /// <summary>
    /// Exports several rigid models into one scene as group node, placement node, mesh
    /// nodes. Every transform is identity because stage HDBs hold world-space geometry.
    /// </summary>
    public static IReadOnlyList<string> ExportMany(
        IReadOnlyList<RigidPlacement> placements, string outputPath,
        string? textureDir = null, bool embed = true)
    {
        var scene = new SceneBuilder();
        var warnings = new List<string>();
        var groups = new Dictionary<string, NodeBuilder>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in placements)
        {
            if (p.Model.Bones.Count > 0)
                warnings.Add($"{p.Name}: has bones; exported rigid (map models are expected rigid)");
            var boneGlobals = BindPose.ComputeGlobals(p.Model.Bones);
            var materials = SceneMaterials.Build(p.Model, textureDir);
            NodeBuilder node;
            if (p.GroupName is null)
            {
                node = new NodeBuilder(p.Name);
            }
            else
            {
                NodeBuilder? parent = null;
                string path = "";
                foreach (string seg in p.GroupName.Split('/', StringSplitOptions.RemoveEmptyEntries))
                {
                    path = path.Length == 0 ? seg : path + "/" + seg;
                    if (!groups.TryGetValue(path, out var g))
                        groups[path] = g = parent is null ? new NodeBuilder(seg) : parent.CreateNode(seg);
                    parent = g;
                }
                node = parent!.CreateNode(p.Name);
            }
            BuildRigidMeshes(p.Model, scene, materials, boneGlobals, node, p.Name);
        }
        var gltf = scene.ToGltf2();
        if (embed) gltf.SaveGLB(outputPath); else gltf.SaveGLTF(outputPath);
        return warnings;
    }

    /// <summary>
    /// Staged (eye) draws go to a second mesh per vertex array because they carry three UV sets.
    /// </summary>
    private static void BuildSkinnedMeshes(ModelFile model, SceneBuilder scene,
        MaterialBuilder[] materials, BoneGlobal[] boneGlobals, NodeBuilder[] boneNodes,
        string? textureDir)
    {
        var stagedMaterials = new Dictionary<(int Stage0, int Stage1, int Stage2), MaterialBuilder>();

        foreach (var va in VertexArrayDraws.Collect(model))
        {
            var mesh = new SkinnedMeshBuilder($"mesh_{va.VAIndex}");
            StagedMeshBuilder? stagedMesh = null;

            foreach (var (group, ia) in va.Draws)
            {
                if (group.Stage1TexIndex >= 0)
                {
                    var key = (group.MaterialIndex, group.Stage1TexIndex, group.Stage2TexIndex);
                    if (!stagedMaterials.TryGetValue(key, out var stagedMat))
                        stagedMaterials[key] = stagedMat = SceneMaterials.BuildStaged(model, key.Item1, key.Item2, key.Item3, textureDir);

                    var stagedVertices = va.Vertices.Select(v => new StagedVertexBuilder(
                        Geometry(v, group.BonePalette, boneGlobals),
                        new VertexTexture3(ExportUV(v.U, v.V), ExportUV(v.UEye, v.VEye), ExportUV(v.UEyelid, v.VEyelid)),
                        Joints(v, group.BonePalette, boneGlobals))).ToList();
                    stagedMesh ??= new StagedMeshBuilder($"mesh_{va.VAIndex}_staged");
                    AddTriangles(stagedMesh.UsePrimitive(stagedMat), stagedVertices, ia.Indices, group.Topology);
                    continue;
                }

                var vertices = va.Vertices.Select(v => new SkinnedVertexBuilder(
                    Geometry(v, group.BonePalette, boneGlobals),
                    new VertexTexture1(ExportUV(v.U, v.V)),
                    Joints(v, group.BonePalette, boneGlobals))).ToList();
                AddTriangles(mesh.UsePrimitive(MaterialFor(materials, group)), vertices, ia.Indices, group.Topology);
            }

            if (mesh.Primitives.Count > 0)
                scene.AddSkinnedMesh(mesh, Matrix4x4.Identity, boneNodes);
            if (stagedMesh is not null)
                scene.AddSkinnedMesh(stagedMesh, Matrix4x4.Identity, boneNodes);
        }
    }

    /// <summary>
    /// Staged draws are multi-texture blends on stage geometry: a base texture plus one or two
    /// layers on their own UV sets. They go to a second mesh per vertex array that keeps all
    /// three UV sets and draws the stage-0 material on TEXCOORD_0. The layer textures are named
    /// in the mesh's sfLayers extra, since glTF has no standard way to blend them.
    /// </summary>
    private static void BuildRigidMeshes(ModelFile model, SceneBuilder scene,
        MaterialBuilder[] materials, BoneGlobal[] boneGlobals,
        NodeBuilder? parent = null, string meshPrefix = "mesh")
    {
        string TextureName(int i) =>
            i >= 0 && i < model.Textures.Count ? model.Textures[i].Name : $"material_{i}";

        foreach (var va in VertexArrayDraws.Collect(model))
        {
            var mesh = new RigidMeshBuilder($"{meshPrefix}_{va.VAIndex}");
            StagedRigidMeshBuilder? stagedMesh = null;
            var layers = new JsonArray();
            var seenLayers = new HashSet<(int, int, int)>();

            foreach (var (group, ia) in va.Draws)
            {
                if (group.Stage1TexIndex >= 0)
                {
                    var stagedVertices = va.Vertices.Select(v => new StagedRigidVertexBuilder(
                        Geometry(v, group.BonePalette, boneGlobals),
                        new VertexTexture3(ExportUV(v.U, v.V), ExportUV(v.UEye, v.VEye), ExportUV(v.UEyelid, v.VEyelid))))
                        .ToList();
                    stagedMesh ??= new StagedRigidMeshBuilder($"{meshPrefix}_{va.VAIndex}_staged");
                    AddTriangles(stagedMesh.UsePrimitive(MaterialFor(materials, group)), stagedVertices, ia.Indices, group.Topology);

                    if (seenLayers.Add((group.MaterialIndex, group.Stage1TexIndex, group.Stage2TexIndex)))
                        layers.Add(new JsonObject
                        {
                            ["stage0"] = TextureName(group.MaterialIndex),
                            ["stage1"] = TextureName(group.Stage1TexIndex),
                            ["stage2"] = group.Stage2TexIndex >= 0 ? TextureName(group.Stage2TexIndex) : null,
                        });
                    continue;
                }

                var vertices = va.Vertices.Select(v => new RigidVertexBuilder(
                    Geometry(v, group.BonePalette, boneGlobals),
                    new VertexTexture1(ExportUV(v.U, v.V)))).ToList();
                AddTriangles(mesh.UsePrimitive(MaterialFor(materials, group)), vertices, ia.Indices, group.Topology);
            }

            if (mesh.Primitives.Count > 0)
                AddRigid(mesh, $"{meshPrefix}_{va.VAIndex}");
            if (stagedMesh is not null)
            {
                stagedMesh.Extras = new JsonObject { ["sfLayers"] = layers };
                AddRigid(stagedMesh, $"{meshPrefix}_{va.VAIndex}_staged");
            }
        }

        void AddRigid(IMeshBuilder<MaterialBuilder> mesh, string nodeName)
        {
            if (parent is null)
                scene.AddRigidMesh(mesh, Matrix4x4.Identity);
            else
                scene.AddRigidMesh(mesh, parent.CreateNode(nodeName));
        }
    }

    private static MaterialBuilder MaterialFor(MaterialBuilder[] materials, MeshGroup group)
        => materials[Math.Clamp(group.MaterialIndex, 0, materials.Length - 1)];

    private static VertexPositionNormal Geometry(Vertex v, List<ushort> palette, BoneGlobal[] boneGlobals)
    {
        var (worldPos, worldNormal) = BindPose.Anchor(v, palette, boneGlobals);
        return new VertexPositionNormal(worldPos, worldNormal);
    }

    private static Vector2 ExportUV(float u, float v) => new(u, 1f - v);

    /// <summary>
    /// JOINTS_0 / WEIGHTS_0 from the vertex influences. An influence whose palette slot or
    /// bone is out of range binds joint 0. Unused slots are (0, 0).
    /// </summary>
    private static VertexJoints4 Joints(Vertex v, List<ushort> palette, BoneGlobal[] boneGlobals)
    {
        Span<(int Joint, float Weight)> binds = stackalloc (int, float)[4];
        binds.Clear();
        int slot = 0;
        foreach (var inf in v.Influences)
        {
            if (slot >= 4) break;
            int palIdx = inf.PaletteIndex;
            int boneIdx = palIdx >= 0 && palIdx < palette.Count ? palette[palIdx] : 0;
            if (boneIdx < 0 || boneIdx >= boneGlobals.Length) boneIdx = 0;
            binds[slot++] = (boneIdx, inf.Weight);
        }
        return new VertexJoints4(binds[0], binds[1], binds[2], binds[3]);
    }

    private static void AddTriangles<TV>(IPrimitiveBuilder prim, List<TV> vertices, ushort[] indices, int topology)
        where TV : IVertexBuilder
    {
        foreach (var (a, b, c) in DrawTriangles.Enumerate(indices, topology, vertices.Count))
            prim.AddTriangle(vertices[a], vertices[b], vertices[c]);
    }
}
