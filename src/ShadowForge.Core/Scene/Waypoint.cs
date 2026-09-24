using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace ShadowForge.Scene;

public sealed class Waypoint
{
    public uint Id { get; set; }

    public WaypointType Type { get; set; }

    /// <summary>
    /// Condition type in bits 0-3, compare operator in bits 4-7.
    /// </summary>
    public uint ConditionFlags { get; set; }

    public uint ConditionValue { get; set; }

    public Vector3 Position { get; set; }

    /// <summary>
    /// Degrees.
    /// </summary>
    public float Rotation { get; set; }

    public uint UnkField20 { get; set; }

    public uint UnkField24 { get; set; }

    public uint UnkField28 { get; set; }

    public uint UnkField2C { get; set; }

    /// <summary>
    /// ASCII name packed big-endian into UnkField20, UnkField24 and UnkField28.
    /// The setter overwrites all three words, so BDSL applies 'ref' before any
    /// @unk_field_20/24/28 override of the same words.
    /// </summary>
    public string ResourceName
    {
        get
        {
            Span<byte> buf = stackalloc byte[12];
            BinaryPrimitives.WriteUInt32BigEndian(buf[0..], UnkField20);
            BinaryPrimitives.WriteUInt32BigEndian(buf[4..], UnkField24);
            BinaryPrimitives.WriteUInt32BigEndian(buf[8..], UnkField28);
            int len = buf.IndexOf((byte)0);
            if (len < 0) len = 12;
            return len > 0 ? Encoding.ASCII.GetString(buf[..len]) : "";
        }
        set
        {
            var padded = new byte[12];
            if (!string.IsNullOrEmpty(value))
                Encoding.ASCII.GetBytes(value, 0, Math.Min(value.Length, 12), padded, 0);
            UnkField20 = BinaryPrimitives.ReadUInt32BigEndian(padded.AsSpan(0));
            UnkField24 = BinaryPrimitives.ReadUInt32BigEndian(padded.AsSpan(4));
            UnkField28 = BinaryPrimitives.ReadUInt32BigEndian(padded.AsSpan(8));
        }
    }

    /// <summary>
    /// The same word as UnkField20.
    /// </summary>
    public uint Priority
    {
        get => UnkField20;
        set => UnkField20 = value;
    }

    public uint LinkCount { get; set; }

    public uint NextWaypoint { get; set; }

    public uint UnkField38 { get; set; }

    public uint ConditionType => ConditionFlags & 0x0F;

    public uint ConditionOp => (ConditionFlags >> 4) & 0x0F;

    public bool IsResource => Type == WaypointType.Resource;

    public bool IsSpawn => Type == WaypointType.Spawn;
}
