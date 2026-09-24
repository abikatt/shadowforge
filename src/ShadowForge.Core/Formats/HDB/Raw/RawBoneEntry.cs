using System.Numerics;
using ShadowForge.Formats.HDB.Wire;

namespace ShadowForge.Formats.HDB.Raw;

/// <summary>
/// Type 7: one node of the first-child / next-sibling bone tree. ChildPtr (+0x38) and
/// SiblingPtr (+0x3C) are signed offsets from their own fields to the target bone's
/// payload. The runtime follows them as stored, so the tree is whatever they describe.
/// Neither Index nor FT order is consulted. ModelWriter recomputes both pointers from
/// ChildFtPos and NextSiblingFtPos and throws when they differ from the values read.
/// </summary>
public sealed class RawBoneEntry : RawEntry
{
    public uint Index { get; set; }
    public uint Reserved04 { get; set; }
    public uint HFlag { get; set; }
    public uint PackedFlags0C { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Euler { get; set; }
    public float Reserved28 { get; set; }
    public Vector3 Scale { get; set; }

    /// <summary>
    /// Index field of the bone ChildPtr targets, or -1. Index is not unique within a
    /// file, so pointer math goes through <see cref="ChildFtPos"/>.
    /// </summary>
    public int ChildIndex { get; set; } = -1;

    /// <summary>
    /// Position of the first child among the file's bone entries, or -1 for a leaf.
    /// </summary>
    public int ChildFtPos { get; set; } = -1;

    internal int ExpectedChildPtrDisk { get; set; }

    internal int ExpectedSiblingPtrDisk { get; set; }

    /// <summary>
    /// Position of the next sibling among the file's bone entries, or -1 for the last sibling.
    /// </summary>
    public int NextSiblingFtPos { get; set; } = -1;

    public string Name { get; set; } = "";

    public float[] ExtraEuler { get; set; } = new float[6];

    /// <summary>
    /// Payload bytes past the 0x68-byte long record, kept verbatim.
    /// </summary>
    public byte[] TrailingBytes { get; set; } = [];

    internal override int ComputedDiskEntryType => (int)FirstTableEntryType.Bone;
    internal override int ComputedPayloadLength => BoneData.LongRecordSize + TrailingBytes.Length;
}
