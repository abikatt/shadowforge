using System.Numerics;
using SharpGLTF.Schema2;
using ShadowForge.Formats.GLTF;
using ShadowForge.Formats.HDB;
using ShadowForge.Formats.HMB;
using ShadowForge.Tests.HDB;

namespace ShadowForge.Tests.GLTF;

public sealed class AnimationExportTests
{
    [Fact]
    public void Export_Bs01WithMot_EmitsNamedAnimations()
    {
        var model = SampleModel.Cooked();
        var clips = MotPack.ReadAll("testdata/anim/bs01_mot.mpk", out _);
        string outPath = Path.Combine(Path.GetTempPath(), $"bs01_anim_{Guid.NewGuid():N}.glb");
        try
        {
            SceneExporter.Export(model, outPath, textureDir: null, embed: true, clips: clips);
            var gltf = ModelRoot.Load(outPath);
            Assert.Equal(clips.Count, gltf.LogicalAnimations.Count);
            var wt = gltf.LogicalAnimations.First(a => a.Name == "bs01_bt_wt01");
            var srcClip = clips.First(c => c.Name == "bs01_bt_wt01");

            float expected = (srcClip.DurationFrames / (float)srcClip.FrameDivisor) / 30f;
            Assert.True(MathF.Abs(wt.Duration - expected) < 0.05f,
                $"duration {wt.Duration} vs expected {expected}");
            Assert.True(wt.Channels.Count > 0);
        }
        finally { File.Delete(outPath); }
    }

    [Fact]
    public void Export_BakedRotation_MatchesSamplerAtFrameTicks()
    {
        var model = SampleModel.Cooked();
        var clips = MotPack.ReadAll("testdata/anim/bs01_mot.mpk", out _);
        var clip = clips.First(c => c.Name == "bs01_bt_wt01");
        string outPath = Path.Combine(Path.GetTempPath(), $"bs01_anim_{Guid.NewGuid():N}.glb");
        try
        {
            SceneExporter.Export(model, outPath, null, true, clips);
            var gltf = ModelRoot.Load(outPath);
            var anim = gltf.LogicalAnimations.First(a => a.Name == clip.Name);

            var track = clip.Tracks.First(t =>
                t.Rotation != null && model.Bones.Any(b => b.Name == t.BoneName));
            var node = gltf.LogicalNodes.First(n => n.Name != null &&
                (n.Name == track.BoneName || n.Name.StartsWith(track.BoneName + "_")));
            var sampler = anim.FindRotationChannel(node)!.GetRotationSampler().CreateCurveSampler();

            for (int frame = 0; frame <= clip.DurationFrames / clip.FrameDivisor; frame += 7)
            {
                var expected = Sampler.SampleTrack(track, frame * clip.FrameDivisor).Rotation!.Value;
                var actual = sampler.GetPoint(frame / 30f);
                Assert.True(MathF.Abs(Quaternion.Dot(expected, actual)) > 0.9999f,
                    $"frame {frame}: {actual} vs {expected}");
            }
        }
        finally { File.Delete(outPath); }
    }

    [Fact]
    public void SandwichRotation_MatchesBindPoseMatrixChain()
    {
        var rnd = new Random(42);
        for (int i = 0; i < 20; i++)
        {
            float Pick() => (float)(rnd.NextDouble() * 4 - 2);
            float preX = Pick(), preY = Pick(), preZ = Pick();
            float animX = Pick(), animY = Pick(), animZ = Pick();
            float postX = Pick(), postY = Pick(), postZ = Pick();

            var qPre = Sampler.EulerToQuaternion(preX, preY, preZ);
            var qAnim = Sampler.EulerToQuaternion(animX, animY, animZ);
            var qPost = Sampler.EulerToQuaternion(postX, postY, postZ);

            var rot2 = BindPose.EulerToMatrix3(preX, preY, preZ);
            var rot1 = BindPose.EulerToMatrix3(animX, animY, animZ);
            var rot3 = BindPose.EulerToMatrix3(postX, postY, postZ);
            var expectedMatrix = BindPose.Mat3Mul(BindPose.Mat3Mul(rot2, rot1), rot3);

            var qSandwich = AnimationExporter.SandwichRotation(qPre, qAnim, qPost);

            foreach (var v in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(1, 2, 3) })
            {
                var byQuat = Vector3.Transform(v, qSandwich);
                var byMat = BindPose.Mat3Vec(expectedMatrix, v);
                Assert.True((byQuat - byMat).Length() < 1e-4f,
                    $"pre({preX},{preY},{preZ}) anim({animX},{animY},{animZ}) post({postX},{postY},{postZ}) " +
                    $"vector {v}: quat {byQuat} != mat {byMat}");
            }
        }
    }

    [Fact]
    public void Export_ImportRoundTrip_PreservesRotationTracks()
    {
        var model = SampleModel.Cooked();
        var clips = MotPack.ReadAll("testdata/anim/bs01_mot.mpk", out _);
        string outPath = Path.Combine(Path.GetTempPath(), $"bs01_rt_{Guid.NewGuid():N}.glb");
        try
        {
            SceneExporter.Export(model, outPath, null, true, clips);
            var gltf = ModelRoot.Load(outPath);

            var orients = new Dictionary<string, (Quaternion Pre, Quaternion Post)>(StringComparer.Ordinal);
            foreach (var bone in model.Bones)
            {
                if (string.IsNullOrEmpty(bone.Name) || orients.ContainsKey(bone.Name)) continue;
                if (bone.ExtraEuler.Any(v => v != 0f))
                {
                    orients[bone.Name] = (
                        Sampler.EulerToQuaternion(bone.ExtraEuler[0], bone.ExtraEuler[1], bone.ExtraEuler[2]),
                        Sampler.EulerToQuaternion(bone.ExtraEuler[3], bone.ExtraEuler[4], bone.ExtraEuler[5]));
                }
            }

            var readBack = ShadowForge.Formats.HDB.Import.GLTFAnimationReader.Read(gltf, out _, orients);
            var srcClip = clips.First(c => c.Name == "bs01_bt_wt01");
            var rtClip = readBack.First(c => c.Name == "bs01_bt_wt01");

            var srcTrack = srcClip.Tracks.First(t => t.BoneName == "shouderparts");
            var rtTrack = rtClip.Tracks.First(t => t.BoneName == "shouderparts");

            var srcKey = Assert.Single(srcTrack.Rotation!.Linear!);
            var rtKey = Assert.Single(rtTrack.Rotation!.Linear!);

            const float AngleScale = 0.000095873802f;
            const float Tolerance = 3f * AngleScale;
            Assert.True(MathF.Abs((short)(rtKey.X - srcKey.X) * AngleScale) < Tolerance,
                $"X: rt={rtKey.X} src={srcKey.X}");
            Assert.True(MathF.Abs((short)(rtKey.Y - srcKey.Y) * AngleScale) < Tolerance,
                $"Y: rt={rtKey.Y} src={srcKey.Y}");
            Assert.True(MathF.Abs((short)(rtKey.Z - srcKey.Z) * AngleScale) < Tolerance,
                $"Z: rt={rtKey.Z} src={srcKey.Z}");
        }
        finally { File.Delete(outPath); }
    }

    [Fact]
    public void Export_BonelessModelWithClips_Warns()
    {
        var model = new ModelFile();
        var clips = new List<MotionClip> { new MotionClip { Name = "test_clip" } };
        string outPath = Path.Combine(Path.GetTempPath(), $"boneless_{Guid.NewGuid():N}.glb");
        try
        {
            var warnings = SceneExporter.Export(model, outPath, null, true, clips);
            Assert.Single(warnings);
            Assert.Contains("model has no bones", warnings[0]);
            Assert.Contains("1", warnings[0]);
        }
        finally
        {
            if (File.Exists(outPath))
                File.Delete(outPath);
        }
    }
}
