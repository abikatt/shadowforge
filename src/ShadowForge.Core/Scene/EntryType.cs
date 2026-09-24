namespace ShadowForge.Scene;

public enum EntryType : uint
{
    Spawn = 0,
    Box = 1,
    Zone = 2,
    Enemy = 3,

    /// <summary>
    /// Event, item or other scripted trigger.
    /// </summary>
    Link = 4,

    Entity = 5,

    /// <summary>
    /// Map transition point.
    /// </summary>
    Warp = 6,
}
