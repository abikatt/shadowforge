namespace ShadowForge.Scene;

public enum WaypointType : uint
{
    Normal = 0,

    /// <summary>
    /// Ref words 0-2 hold an ASCII resource name.
    /// </summary>
    Resource = 5,

    /// <summary>
    /// Ref word 0 holds a spawn priority.
    /// </summary>
    Spawn = 6,
}
