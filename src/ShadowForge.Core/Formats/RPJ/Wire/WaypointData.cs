using System.Numerics;
using ShadowForge.Marshal;
using ShadowForge.Scene;

namespace ShadowForge.Formats.RPJ.Wire;

[BigEndian, StructSize(0x40)]
public partial struct WaypointData
{
    public uint Id;
    public WaypointType Type;
    public uint ConditionFlags;
    public uint ConditionValue;
    public Vector3 Position;
    public float Rotation;
    public uint UnkField20;
    public uint UnkField24;
    public uint UnkField28;
    public uint UnkField2C;
    public uint LinkCount;
    [Computed] public uint NextWaypoint;
    public uint UnkField38;
    [Computed] public uint NextOffset;

    public Waypoint ToModel()
    {
        return new Waypoint
        {
            Id = Id,
            Type = Type,
            ConditionFlags = ConditionFlags,
            ConditionValue = ConditionValue,
            Position = Position,
            Rotation = Rotation,
            UnkField20 = UnkField20,
            UnkField24 = UnkField24,
            UnkField28 = UnkField28,
            UnkField2C = UnkField2C,
            LinkCount = LinkCount,
            NextWaypoint = NextWaypoint,
            UnkField38 = UnkField38,
        };
    }

    public static WaypointData FromModel(Waypoint model)
    {
        return new WaypointData
        {
            Id = model.Id,
            Type = model.Type,
            ConditionFlags = model.ConditionFlags,
            ConditionValue = model.ConditionValue,
            Position = model.Position,
            Rotation = model.Rotation,
            UnkField20 = model.UnkField20,
            UnkField24 = model.UnkField24,
            UnkField28 = model.UnkField28,
            UnkField2C = model.UnkField2C,
            LinkCount = model.LinkCount,
            NextWaypoint = model.NextWaypoint,
            UnkField38 = model.UnkField38,
        };
    }
}
