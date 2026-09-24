using System.Numerics;
using ShadowForge.Marshal;

namespace ShadowForge.Formats.HDB.Wire;

[BigEndian, StructSize(0x68)]
public partial struct BoneData
{
    /// <summary>
    /// Record size when HFlag carries <see cref="BoneFlags.ExtraEulerA"/> or
    /// <see cref="BoneFlags.ExtraEulerB"/>.
    /// </summary>
    public const int LongRecordSize = 104;

    /// <summary>
    /// Record size without the extra-euler triples. The record ends after the name field.
    /// </summary>
    public const int ShortRecordSize = 80;

    public const int NameWidth = 16;

    public const int IndexOffset = 0x00;
    public const int FlagsOffset = 0x08;
    public const int ModelPtrOffset = 0x0C;
    public const int PositionOffset = 0x10;
    public const int EulerOffset = 0x1C;
    public const int ScaleOffset = 0x2C;
    public const int ChildPtrOffset = 0x38;
    public const int SiblingPtrOffset = 0x3C;
    public const int NameOffset = 0x40;
    public const int ExtraEulerAOffset = 0x50;
    public const int ExtraEulerBOffset = 0x5C;

    public uint Index;
    public uint Reserved04;
    public uint HFlag;
    public uint PackedFlags0C;
    public Vector3 Position;
    public Vector3 Euler;
    public float Reserved28;
    public Vector3 Scale;
    public int ChildPtr;
    public int SiblingPtr;
    [EncodedString(16, StringEncoding.ASCII)]
    public byte[] Name;
    public float ExtraEuler0;
    public float ExtraEuler1;
    public float ExtraEuler2;
    public float ExtraEuler3;
    public float ExtraEuler4;
    public float ExtraEuler5;
}
