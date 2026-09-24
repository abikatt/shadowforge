using System.Numerics;
using SharpGLTF.Scenes;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using ShadowForge.Formats.HDB.Import;
using ShadowForge.Formats.HMB;

namespace ShadowForge.Tests.HMB;

public sealed class GLTFAnimationReaderTests
{
    private static SharpGLTF.Schema2.ModelRoot BuildScene(
        IReadOnlyDictionary<float, Quaternion>? rotKeys,
        IReadOnlyDictionary<float, Vector3>? transKeys)
    {
        var root = new NodeBuilder("root");
        var child = root.CreateNode("child");
        if (rotKeys != null) child.WithLocalRotation("clipA", rotKeys);
        if (transKeys != null) child.WithLocalTranslation("clipA", transKeys);

        var mesh = new MeshBuilder<VertexPositionNormal, VertexTexture1, VertexJoints4>("m");
        var prim = mesh.UsePrimitive(SharpGLTF.Materials.MaterialBuilder.CreateDefault());
        VertexBuilder<VertexPositionNormal, VertexTexture1, VertexJoints4> V(float x) =>
            new(new VertexPositionNormal(x, 0, 0, 0, 1, 0), new VertexTexture1(Vector2.Zero),
                new VertexJoints4((0, 1f)));
        prim.AddTriangle(V(0), V(1), V(2));

        var scene = new SceneBuilder();
        scene.AddNode(root);
        scene.AddSkinnedMesh(mesh, Matrix4x4.Identity, root, child);
        return scene.ToGltf2();
    }

    [Fact]
    public void Read_RotationRoundTrips_ThroughEulerQuantization()
    {
        var keys = new Dictionary<float, Quaternion>
        {
            [0f] = Sampler.EulerToQuaternion(0, 0, 0),
            [1f] = Sampler.EulerToQuaternion(0, 0, MathF.PI / 2f),
        };
        var gltf = BuildScene(keys, null);
        var clips = GLTFAnimationReader.Read(gltf, out var warnings);

        var clip = Assert.Single(clips);
        Assert.Equal("clipA", clip.Name);
        Assert.Equal(2, clip.Mode);
        Assert.Equal(30, clip.DurationFrames);

        var track = clip.Tracks.First(t => t.BoneName == "child");
        var quantized = track.Rotation!.Linear!;

        var last = quantized[^1];
        Assert.Equal(30, last.Frame);
        Assert.InRange(last.Z, (short)16383, (short)16385);
        Assert.Equal(0, last.X);
        Assert.Equal(0, last.Y);

        Assert.True(quantized.Length <= 4, $"expected reduced keys, got {quantized.Length}");
    }

    [Fact]
    public void Read_ConstantChannel_CollapsesToOneKey()
    {
        var keys = new Dictionary<float, Vector3> { [0f] = new(1, 2, 3), [1f] = new(1, 2, 3) };
        var gltf = BuildScene(null, keys);
        var clips = GLTFAnimationReader.Read(gltf, out _);
        var track = clips[0].Tracks.First(t => t.BoneName == "child");
        Assert.Single(track.Translation!.Linear!);
        Assert.Equal(new Vector3(1, 2, 3), track.Translation!.Linear![0].Value);
        Assert.Null(track.Rotation);
    }

    [Fact]
    public void Read_SustainedRotation_CrossesPiWithoutThrowing()
    {
        var keys = new Dictionary<float, Quaternion>
        {
            [0f] = Sampler.EulerToQuaternion(0, 0, 0),
            [2f / 3f] = Sampler.EulerToQuaternion(0, 0, 0.5f * MathF.PI),
            [4f / 3f] = Sampler.EulerToQuaternion(0, 0, MathF.PI),
            [2f] = Sampler.EulerToQuaternion(0, 0, 1.5f * MathF.PI),
        };
        var gltf = BuildScene(keys, null);

        var clips = GLTFAnimationReader.Read(gltf, out _);

        var clip = Assert.Single(clips);
        var track = clip.Tracks.First(t => t.BoneName == "child");
        var quantized = track.Rotation!.Linear!;

        float angle = 0f;
        for (int i = 1; i < quantized.Length; i++)
        {
            int d = quantized[i].Z - quantized[i - 1].Z;
            if (d < -0x8000) d += 0x10000;
            else if (d > 0x8000) d -= 0x10000;
            angle += d * RawAngle.Scale;
        }

        Assert.InRange(angle, 1.5f * MathF.PI - 0.01f, 1.5f * MathF.PI + 0.01f);
    }

    [Fact]
    public void Read_GimbalLockPitch_DoesNotThrow()
    {
        var keys = new Dictionary<float, Quaternion>
        {
            [0f] = Sampler.EulerToQuaternion(0, 0, 0),
            [1f] = Sampler.EulerToQuaternion(0, MathF.PI / 2f, 0),
            [2f] = Sampler.EulerToQuaternion(0.3f, MathF.PI / 2f, 0.3f),
        };
        var gltf = BuildScene(keys, null);

        var clips = GLTFAnimationReader.Read(gltf, out _);

        var clip = Assert.Single(clips);
        var track = clip.Tracks.First(t => t.BoneName == "child");
        Assert.NotNull(track.Rotation);
        Assert.NotEmpty(track.Rotation!.Linear!);
    }

    [Fact]
    public void SandwichRoundTrip_RecoversAnimRotation()
    {
        var qPre = Sampler.EulerToQuaternion(0.4f, -0.2f, 0.1f);
        var qPost = Sampler.EulerToQuaternion(-0.3f, 0.25f, -0.15f);

        float animZ0 = 0f;
        float animZ1 = MathF.PI / 2f;
        var qAnim0 = Sampler.EulerToQuaternion(0, 0, animZ0);
        var qAnim1 = Sampler.EulerToQuaternion(0, 0, animZ1);

        var rotKeys = new Dictionary<float, Quaternion>
        {
            [0f] = ShadowForge.Formats.GLTF.AnimationExporter.SandwichRotation(qPre, qAnim0, qPost),
            [1f] = ShadowForge.Formats.GLTF.AnimationExporter.SandwichRotation(qPre, qAnim1, qPost),
        };
        var gltf = BuildScene(rotKeys, null);

        var orients = new Dictionary<string, (Quaternion Pre, Quaternion Post)>
        {
            ["child"] = (qPre, qPost),
        };
        var clips = GLTFAnimationReader.Read(gltf, out _, orients);

        var clip = Assert.Single(clips);
        var track = clip.Tracks.First(t => t.BoneName == "child");
        var quantized = track.Rotation!.Linear!;

        short ExpectedRaw(float angle) => (short)(((int)MathF.Round(angle / RawAngle.Scale)) & 0xFFFF);

        var first = quantized[0];
        var last = quantized[^1];
        Assert.Equal(0, first.X);
        Assert.Equal(0, first.Y);
        Assert.InRange((short)(first.Z - ExpectedRaw(animZ0)), (short)-2, (short)2);
        Assert.Equal(0, last.X);
        Assert.Equal(0, last.Y);
        Assert.InRange((short)(last.Z - ExpectedRaw(animZ1)), (short)-2, (short)2);
    }

    [Fact]
    public void Read_JointNameTooLong_Throws()
    {
        var root = new NodeBuilder("root");
        var child = root.CreateNode("this_name_is_way_too_long_for_hmb");
        child.WithLocalTranslation("clipA",
            new Dictionary<float, Vector3> { [0f] = Vector3.Zero, [1f] = Vector3.One });
        var mesh = new MeshBuilder<VertexPositionNormal, VertexTexture1, VertexJoints4>("m");
        var prim = mesh.UsePrimitive(SharpGLTF.Materials.MaterialBuilder.CreateDefault());
        VertexBuilder<VertexPositionNormal, VertexTexture1, VertexJoints4> V(float x) =>
            new(new VertexPositionNormal(x, 0, 0, 0, 1, 0), new VertexTexture1(Vector2.Zero),
                new VertexJoints4((0, 1f)));
        prim.AddTriangle(V(0), V(1), V(2));
        var scene = new SceneBuilder();
        scene.AddNode(root);
        scene.AddSkinnedMesh(mesh, Matrix4x4.Identity, root, child);

        Assert.Throws<InvalidDataException>(() =>
            GLTFAnimationReader.Read(scene.ToGltf2(), out _));
    }

    [Fact]
    public void CrossCheckSkeleton_MatchingSets_NoWarnings()
    {
        var gltf = BuildScene(null, new Dictionary<float, Vector3> { [0f] = Vector3.Zero, [1f] = Vector3.One });
        var warnings = GLTFAnimationReader.CrossCheckSkeleton(new[] { "root", "child" }, gltf);
        Assert.Empty(warnings);
    }

    [Fact]
    public void CrossCheckSkeleton_HDBOnlyBone_WarnsStaleHDB()
    {
        var gltf = BuildScene(null, new Dictionary<float, Vector3> { [0f] = Vector3.Zero, [1f] = Vector3.One });
        var warnings = GLTFAnimationReader.CrossCheckSkeleton(
            new[] { "root", "child", "leftmouthcorner" }, gltf);
        var w = Assert.Single(warnings);
        Assert.Contains("leftmouthcorner", w);
        Assert.Contains("STALE", w);
    }

    [Fact]
    public void CrossCheckSkeleton_GLTFOnlyJoint_Warns()
    {
        var gltf = BuildScene(null, new Dictionary<float, Vector3> { [0f] = Vector3.Zero, [1f] = Vector3.One });
        var warnings = GLTFAnimationReader.CrossCheckSkeleton(new[] { "root" }, gltf);
        var w = Assert.Single(warnings);
        Assert.Contains("child", w);
        Assert.Contains("cannot bind", w);
    }
}
