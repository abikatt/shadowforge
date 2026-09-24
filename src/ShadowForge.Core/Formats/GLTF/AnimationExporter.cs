using System.Numerics;
using SharpGLTF.Scenes;
using ShadowForge.Formats.HDB;
using ShadowForge.Formats.HMB;

namespace ShadowForge.Formats.GLTF;

public static class AnimationExporter
{
    public static List<string> AddClips(
        NodeBuilder[] boneNodes, IReadOnlyList<Bone> bones, IReadOnlyList<MotionClip> clips)
    {
        var warnings = new List<string>();

        var byName = new Dictionary<string, int>();
        for (int i = 0; i < bones.Count; i++)
            if (!string.IsNullOrEmpty(bones[i].Name))
                byName.TryAdd(bones[i].Name, i);

        var orients = new Dictionary<int, (Quaternion Pre, Quaternion Post)>();
        for (int i = 0; i < bones.Count; i++)
        {
            var extra = bones[i].ExtraEuler;
            if (extra.Any(v => v != 0f))
            {
                orients[i] = (
                    Sampler.EulerToQuaternion(extra[0], extra[1], extra[2]),
                    Sampler.EulerToQuaternion(extra[3], extra[4], extra[5]));
            }
        }

        var mirrors = SkeletonBuilder.ComputeAccumulatedMirrors(bones);

        var unmatchedClips = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var clip in clips)
        {
            if (clip.ExtraSectionTypes.Count > 0)
                warnings.Add($"{clip.Name}: unhandled extra FT sections " +
                    string.Join(", ", clip.ExtraSectionTypes.Select(t => $"0x{t:X}")));

            int divisor = clip.FrameDivisor;
            int wallFrames = Math.Max(1, clip.DurationFrames / divisor);

            foreach (var track in clip.Tracks)
            {
                if (!byName.TryGetValue(track.BoneName, out int boneIdx))
                {
                    if (!unmatchedClips.TryGetValue(track.BoneName, out var clipSet))
                        unmatchedClips[track.BoneName] = clipSet = new HashSet<string>(StringComparer.Ordinal);
                    clipSet.Add(clip.Name);
                    continue;
                }
                var node = boneNodes[boneIdx];
                orients.TryGetValue(boneIdx, out var orient);
                int parentIdx = bones[boneIdx].ParentIndex;
                var parentMirror = parentIdx >= 0 && parentIdx < mirrors.Length
                    ? mirrors[parentIdx]
                    : Vector3.One;
                var ownSign = parentIdx >= 0 && parentIdx < mirrors.Length
                    ? mirrors[boneIdx] * mirrors[parentIdx]
                    : mirrors[boneIdx];

                var tKeys = track.Translation != null ? new Dictionary<float, Vector3>() : null;
                var rKeys = track.Rotation != null ? new Dictionary<float, Quaternion>() : null;
                var sKeys = track.Scale != null ? new Dictionary<float, Vector3>() : null;

                for (int wall = 0; wall <= wallFrames; wall++)
                {
                    float time = wall / MotionClip.DefaultFrameRate;
                    var pose = Sampler.SampleTrack(track, wall * divisor);
                    if (tKeys != null) tKeys[time] = parentMirror * pose.Translation!.Value;
                    if (rKeys != null)
                    {
                        var qAnim = pose.Rotation!.Value;
                        var q = orient.Pre == default && orient.Post == default
                            ? qAnim
                            : SandwichRotation(orient.Pre, qAnim, orient.Post);
                        rKeys[time] = ConjugateByMirror(q, parentMirror);
                    }
                    if (sKeys != null) sKeys[time] = ownSign * pose.Scale!.Value;
                }

                if (tKeys != null) node.WithLocalTranslation(clip.Name, tKeys);
                if (rKeys != null) node.WithLocalRotation(clip.Name, rKeys);
                if (sKeys != null) node.WithLocalScale(clip.Name, sKeys);
            }
        }

        if (unmatchedClips.Count > 0)
        {
            var parts = unmatchedClips
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"'{kv.Key}' ({kv.Value.Count} clip(s))");
            warnings.Add(
                $"{unmatchedClips.Count} track bone name(s) had no matching bone: " + string.Join(", ", parts));
        }

        return warnings;
    }

    /// <summary>
    /// local = pre * anim * post for column vectors, the quaternion form of
    /// <see cref="BindPose.LocalRotation"/> with the animated rotation in place of Euler.
    /// </summary>
    internal static Quaternion SandwichRotation(Quaternion pre, Quaternion anim, Quaternion post)
        => Quaternion.Concatenate(Quaternion.Concatenate(post, anim), pre);

    /// <summary>
    /// Quaternion of A * R * A for a diagonal +-1 mirror A: (x*ay*az, y*ax*az, z*ax*ay, w).
    /// This holds for proper and reflecting mirrors alike, and matches the matrix form in
    /// <see cref="SkeletonBuilder"/>.
    /// </summary>
    internal static Quaternion ConjugateByMirror(Quaternion q, Vector3 a)
        => a == Vector3.One
            ? q
            : new Quaternion(q.X * a.Y * a.Z, q.Y * a.X * a.Z, q.Z * a.X * a.Y, q.W);
}
