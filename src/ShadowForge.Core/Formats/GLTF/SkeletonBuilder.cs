using System.Numerics;
using ShadowForge.Formats.HDB;
using SharpGLTF.Scenes;

namespace ShadowForge.Formats.GLTF;

/// <summary>
/// Builds the glTF joint hierarchy for a model's bones. Negative bone scales cannot be
/// stored in a node, so each node takes the absolute scale and its translation and
/// rotation are conjugated by the mirror its ancestors accumulate. A node's world
/// transform is then the runtime world times that mirror, which skins identically.
/// </summary>
internal static class SkeletonBuilder
{
    /// <summary>
    /// Returns one node per bone, in bone order. Bones without a valid parent hang under
    /// an "Armature" node, which is not a joint. Empty or duplicate names get the bone
    /// index appended, since glTF armatures reject duplicate names.
    /// </summary>
    public static NodeBuilder[] Build(ModelFile model, SceneBuilder scene)
    {
        if (model.Bones.Count == 0) return Array.Empty<NodeBuilder>();

        var nameCounts = new Dictionary<string, int>();
        foreach (var bone in model.Bones)
            nameCounts[bone.Name] = nameCounts.GetValueOrDefault(bone.Name) + 1;

        var nodes = new NodeBuilder[model.Bones.Count];
        for (int i = 0; i < model.Bones.Count; i++)
        {
            var rawName = model.Bones[i].Name;
            var name = (string.IsNullOrEmpty(rawName) || nameCounts[rawName] > 1)
                ? $"{(string.IsNullOrEmpty(rawName) ? "bone" : rawName)}_{i}"
                : rawName;
            nodes[i] = new NodeBuilder(name);
        }

        var armature = new NodeBuilder("Armature");
        scene.AddNode(armature);

        var mirrors = ComputeAccumulatedMirrors(model.Bones);

        for (int i = 0; i < model.Bones.Count; i++)
        {
            var bone = model.Bones[i];
            var node = nodes[i];
            var parentMirror = bone.ParentIndex >= 0 && bone.ParentIndex < mirrors.Length
                ? mirrors[bone.ParentIndex]
                : Vector3.One;

            node.UseTranslation().Value = parentMirror * new Vector3(bone.PosX, bone.PosY, bone.PosZ);

            bool allScaleZero = bone.ScaleX == 0f && bone.ScaleY == 0f && bone.ScaleZ == 0f;
            node.UseScale().Value = allScaleZero
                ? Vector3.One
                : Vector3.Abs(new Vector3(bone.ScaleX, bone.ScaleY, bone.ScaleZ));

            node.UseRotation().Value = Mat3ToQuaternion(ConjugateByMirror(BindPose.LocalRotation(bone), parentMirror));

            if (bone.ParentIndex >= 0 && bone.ParentIndex < nodes.Length)
                nodes[bone.ParentIndex].AddNode(node);
            else
                armature.AddNode(node);
        }

        return nodes;
    }

    /// <summary>
    /// Per-bone product of sign(scale) down the parent chain. A zero component counts as
    /// +1, so zero-scale placeholder bones stay neutral.
    /// </summary>
    public static Vector3[] ComputeAccumulatedMirrors(IReadOnlyList<Bone> bones)
    {
        var mirrors = new Vector3[bones.Count];
        var resolved = new bool[bones.Count];

        bool madeProgress = true;
        while (madeProgress)
        {
            madeProgress = false;
            for (int i = 0; i < bones.Count; i++)
            {
                if (resolved[i]) continue;
                var bone = bones[i];
                bool parentReady = bone.ParentIndex < 0
                                || bone.ParentIndex >= bones.Count
                                || resolved[bone.ParentIndex];
                if (!parentReady) continue;
                var parent = bone.ParentIndex >= 0 && bone.ParentIndex < bones.Count
                    ? mirrors[bone.ParentIndex]
                    : Vector3.One;
                mirrors[i] = parent * SignVector(bone);
                resolved[i] = true;
                madeProgress = true;
            }
        }
        for (int i = 0; i < bones.Count; i++)
            if (!resolved[i])
                mirrors[i] = SignVector(bones[i]);

        return mirrors;

        static Vector3 SignVector(Bone b) => new(
            b.ScaleX < 0f ? -1f : 1f,
            b.ScaleY < 0f ? -1f : 1f,
            b.ScaleZ < 0f ? -1f : 1f);
    }

    /// <summary>
    /// A * M * A for a diagonal +-1 mirror A, so (A * M * A)[r, c] = a_r * M[r, c] * a_c.
    /// det(A * M * A) = det(M), so a rotation stays a proper rotation.
    /// </summary>
    private static float[,] ConjugateByMirror(float[,] m, Vector3 a)
    {
        if (a == Vector3.One) return m;
        Span<float> s = stackalloc float[3] { a.X, a.Y, a.Z };
        var r = new float[3, 3];
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                r[i, j] = s[i] * m[i, j] * s[j];
        return r;
    }

    private static Quaternion Mat3ToQuaternion(float[,] m)
    {
        var m4 = new Matrix4x4(
            m[0, 0], m[1, 0], m[2, 0], 0,
            m[0, 1], m[1, 1], m[2, 1], 0,
            m[0, 2], m[1, 2], m[2, 2], 0,
            0, 0, 0, 1);
        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(m4));
    }
}
