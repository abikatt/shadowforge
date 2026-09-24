namespace ShadowForge.Formats.HDB.Raw;

/// <summary>
/// Bone entries in first-table order with their payload positions. A bone's FT
/// position is its index in <see cref="Bones"/>. FT order does not constrain the tree:
/// retail em003 has a bone at FT 6 whose first child sits at FT 2.
/// </summary>
internal sealed class BoneLayout
{
    public IReadOnlyList<RawBoneEntry> Bones { get; }
    public int[] FtToPayloadPos { get; }
    private readonly Dictionary<RawBoneEntry, int> _ftByBone;

    private BoneLayout(IReadOnlyList<RawBoneEntry> bones, int[] ftToPayloadPos, Dictionary<RawBoneEntry, int> ftByBone)
    {
        Bones = bones;
        FtToPayloadPos = ftToPayloadPos;
        _ftByBone = ftByBone;
    }

    public int GetFt(RawBoneEntry bone) => _ftByBone[bone];

    public static BoneLayout Build(RawModel raw)
    {
        var bones = raw.FirstTable.OfType<RawBoneEntry>().ToList();
        var pos = new int[bones.Count];
        var ftByBone = new Dictionary<RawBoneEntry, int>(bones.Count, ReferenceEqualityComparer.Instance);
        for (int ft = 0; ft < bones.Count; ft++)
        {
            pos[ft] = bones[ft].PayloadPos;
            ftByBone[bones[ft]] = ft;
        }
        return new BoneLayout(bones, pos, ftByBone);
    }

    /// <summary>
    /// Disk ChildPtr of the bone at <paramref name="ft"/>: zero for a leaf, otherwise the
    /// offset from its +0x38 field to the first child's payload.
    /// </summary>
    public int ComputeChildPtr(int ft)
    {
        var bone = Bones[ft];
        if (bone.ChildFtPos < 0) return 0;
        if (bone.ChildFtPos >= Bones.Count)
            throw new InvalidDataException(
                $"Bone FT={ft} Index={bone.Index}: ChildFtPos={bone.ChildFtPos} " +
                $"out of range (bone count {Bones.Count}).");
        return FtToPayloadPos[bone.ChildFtPos] - (FtToPayloadPos[ft] + Wire.BoneData.ChildPtrOffset);
    }

    /// <summary>
    /// Disk SiblingPtr of the bone at <paramref name="ft"/>: zero for the last sibling,
    /// otherwise the offset from its +0x3C field to the next sibling's payload.
    /// </summary>
    public int ComputeSiblingPtr(int ft)
    {
        int next = Bones[ft].NextSiblingFtPos;
        if (next < 0) return 0;
        return FtToPayloadPos[next] - (FtToPayloadPos[ft] + Wire.BoneData.SiblingPtrOffset);
    }
}
