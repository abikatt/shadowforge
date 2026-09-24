using System.Numerics;
using SharpGLTF.Schema2;
using ShadowForge.Formats.GLTF;
using ShadowForge.Formats.HDB;
using ShadowForge.Formats.HMB;

namespace ShadowForge.Tests.GLTF;

public sealed class MirroredBoneExportTests
{
    private static ModelFile MirroredRig() => new()
    {
        Bones =
        {
            new Bone { Index = 0, Name = "head", ParentIndex = -1 },
            new Bone
            {
                Index = 1, Name = "rightcheek", ParentIndex = 0,
                PosX = 1.0918f, PosY = 0.8790f, PosZ = 1.5898f,
                ScaleX = 1f, ScaleY = 1f, ScaleZ = -1f,
                ExtraEuler = new float[] { 0f, 0f, -1.5708f, 0f, 0f, 0f },
            },
            new Bone
            {
                Index = 2, Name = "cheekchild", ParentIndex = 1,
                PosX = 0.5f, PosY = 0.25f, PosZ = -0.125f,
                EulerX = 0.3f, EulerY = 0.2f, EulerZ = 0.1f,
            },
        },
    };

    private static Matrix4x4 RuntimeWorld(ModelFile model, int boneIdx)
    {
        var globals = BindPose.ComputeGlobals(model.Bones);
        var g = globals[boneIdx];
        var m = Matrix4x4.Identity;

        m.M11 = g.Matrix[0, 0]; m.M21 = g.Matrix[0, 1]; m.M31 = g.Matrix[0, 2];
        m.M12 = g.Matrix[1, 0]; m.M22 = g.Matrix[1, 1]; m.M32 = g.Matrix[1, 2];
        m.M13 = g.Matrix[2, 0]; m.M23 = g.Matrix[2, 1]; m.M33 = g.Matrix[2, 2];
        m.Translation = g.Position;
        return m;
    }

    private static Matrix4x4 NodeWorld(Node node)
        => node.VisualParent == null
            ? node.LocalMatrix
            : node.LocalMatrix * NodeWorld(node.VisualParent);

    [Fact]
    public void MirroredBone_ExportsOrthonormalJoints_WorldEquivalentUpToMirror()
    {
        var model = MirroredRig();
        string outPath = Path.Combine(Path.GetTempPath(), $"mirror_{Guid.NewGuid():N}.glb");
        try
        {
            SceneExporter.Export(model, outPath);
            var gltf = ModelRoot.Load(outPath);

            var mirrors = new[] { Vector3.One, new Vector3(1, 1, -1), new Vector3(1, 1, -1) };

            for (int i = 0; i < model.Bones.Count; i++)
            {
                var bone = model.Bones[i];
                var node = gltf.LogicalNodes.First(n => n.Name == bone.Name);

                var s = node.LocalTransform.Scale;
                Assert.True(s.X > 0 && s.Y > 0 && s.Z > 0,
                    $"{bone.Name}: joint rest scale {s} must be positive");

                var expected = Matrix4x4.CreateScale(mirrors[i]) * RuntimeWorld(model, i);
                var actual = NodeWorld(node);
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < 4; c++)
                        Assert.True(MathF.Abs(actual[r, c] - expected[r, c]) < 1e-4f,
                            $"{bone.Name}[{r},{c}]: {actual[r, c]} vs {expected[r, c]}");
            }
        }
        finally { File.Delete(outPath); }
    }

    [Fact]
    public void MirroredBone_AnimatedChild_TracksConjugatedByParentMirror()
    {
        var model = MirroredRig();

        var clip = new MotionClip
        {
            Name = "test_clip",
            Mode = 2,
            DurationFrames = 2,
            Tracks =
            {
                new MotionTrack
                {
                    BoneName = "cheekchild",
                    Translation = new Vec3Channel
                        { Linear = new[] { new LinearVec3Key(0, new Vector3(0.7f, -0.2f, 0.4f)) } },
                    Rotation = new EulerChannel
                        { Linear = new[] { new LinearEulerKey(
                                0,
                                RawAngle.FromRadians(0.4f),
                                RawAngle.FromRadians(-0.3f),
                                RawAngle.FromRadians(0.2f)) } },
                },
            },
        };

        string outPath = Path.Combine(Path.GetTempPath(), $"mirroranim_{Guid.NewGuid():N}.glb");
        try
        {
            SceneExporter.Export(model, outPath, null, true, new[] { clip });
            var gltf = ModelRoot.Load(outPath);
            var node = gltf.LogicalNodes.First(n => n.Name == "cheekchild");
            var anim = gltf.LogicalAnimations.First(a => a.Name == "test_clip");

            var mirror = new Vector3(1, 1, -1);

            var tSampler = anim.FindTranslationChannel(node)!.GetTranslationSampler().CreateCurveSampler();
            var tBaked = tSampler.GetPoint(0f);
            var tExpected = mirror * new Vector3(0.7f, -0.2f, 0.4f);
            Assert.True((tBaked - tExpected).Length() < 1e-4f, $"translation {tBaked} vs {tExpected}");

            var rSampler = anim.FindRotationChannel(node)!.GetRotationSampler().CreateCurveSampler();
            var qBaked = rSampler.GetPoint(0f);
            var animM = BindPose.EulerToMatrix3(0.4f, -0.3f, 0.2f);
            foreach (var v in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(1, 2, 3) })
            {
                var expected = mirror * BindPose.Mat3Vec(animM, mirror * v);
                var actual = Vector3.Transform(v, qBaked);
                Assert.True((actual - expected).Length() < 1e-3f,
                    $"vector {v}: baked {actual} vs conjugated {expected}");
            }
        }
        finally { File.Delete(outPath); }
    }
}
