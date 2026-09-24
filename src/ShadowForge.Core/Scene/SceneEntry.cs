using System.Numerics;
using ShadowForge.Scene.Script;

namespace ShadowForge.Scene;

/// <summary>
/// A placed scene entry such as a spawn point, trigger box or warp.
/// </summary>
public sealed class SceneEntry
{
    public uint Id { get; set; }

    /// <summary>
    /// The 24-byte Shift-JIS name field. Embedded NULs become U+F000 so the whole field round-trips.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Filled in by the game at load. Zero in shipped files.
    /// </summary>
    public uint RuntimeRef { get; set; }

    public uint RefId { get; set; }

    public EntryType Type { get; set; }

    public Vector3 Position { get; set; }

    /// <summary>
    /// Degrees.
    /// </summary>
    public float Facing { get; set; }

    /// <summary>
    /// Box half-extents for a box, the route for an enemy, and three raw target words for every other type.
    /// </summary>
    public Vector3 Extents { get; set; }

    /// <summary>
    /// Yaw for a box, aggro radius for an enemy, rotation for a warp.
    /// </summary>
    public float TriggerRadius { get; set; }

    public uint UnkField48 { get; set; }

    public uint UnkField4C { get; set; }

    public uint UnkField50 { get; set; }

    public uint UnkField54 { get; set; }

    public uint UnkField58 { get; set; }

    public uint UnkField5C { get; set; }

    public uint UnkField60 { get; set; }

    public List<ScriptBlock> ScriptBlocks { get; set; } = [];
}
