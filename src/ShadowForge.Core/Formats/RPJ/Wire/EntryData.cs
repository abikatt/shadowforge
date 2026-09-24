using System.Numerics;
using ShadowForge.Marshal;
using ShadowForge.Scene;

namespace ShadowForge.Formats.RPJ.Wire;

[BigEndian, StructSize(0x70)]
public partial struct EntryData
{
    public uint Id;
    [EncodedString(24, StringEncoding.ShiftJIS)]
    public byte[] RawNameField;
    public uint RuntimeRef;
    public uint RefId;
    public EntryType Type;
    public Vector3 Position;
    public float Facing;
    public Vector3 Extents;
    public float TriggerRadius;
    public uint UnkField48;
    public uint UnkField4C;
    public uint UnkField50;
    public uint UnkField54;
    public uint UnkField58;
    public uint UnkField5C;
    public uint UnkField60;
    [Computed] public uint ScriptBlockCount;
    [Computed] public uint ScriptBlockOffset;
    [Computed] public uint NextEntry;

    public SceneEntry ToModel()
    {
        return new SceneEntry
        {
            Id = Id,
            Name = RawNameField.DecodeShiftJISFull(0, 24),
            RuntimeRef = RuntimeRef,
            RefId = RefId,
            Type = Type,
            Position = Position,
            Facing = Facing,
            Extents = Extents,
            TriggerRadius = TriggerRadius,
            UnkField48 = UnkField48,
            UnkField4C = UnkField4C,
            UnkField50 = UnkField50,
            UnkField54 = UnkField54,
            UnkField58 = UnkField58,
            UnkField5C = UnkField5C,
            UnkField60 = UnkField60,
        };
    }

    public static EntryData FromModel(SceneEntry model)
    {
        var nameBytes = new byte[24];
        model.Name.WriteShiftJIS(nameBytes, 0, 24);
        return new EntryData
        {
            Id = model.Id,
            RawNameField = nameBytes,
            RuntimeRef = model.RuntimeRef,
            RefId = model.RefId,
            Type = model.Type,
            Position = model.Position,
            Facing = model.Facing,
            Extents = model.Extents,
            TriggerRadius = model.TriggerRadius,
            UnkField48 = model.UnkField48,
            UnkField4C = model.UnkField4C,
            UnkField50 = model.UnkField50,
            UnkField54 = model.UnkField54,
            UnkField58 = model.UnkField58,
            UnkField5C = model.UnkField5C,
            UnkField60 = model.UnkField60,
        };
    }
}
