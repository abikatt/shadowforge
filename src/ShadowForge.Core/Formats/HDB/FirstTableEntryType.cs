namespace ShadowForge.Formats.HDB;

/// <summary>
/// Type code in the first word of a first-table entry. The loader switches
/// on it to decide how to interpret the entry's payload.
/// </summary>
public enum FirstTableEntryType : uint
{
    Padding = 0,
    U32Array = 1,
    U16Array = 2,
    RenderCommands = 3,
    IndexTable = 4,
    VASetup = 5,
    Model = 6,
    Bone = 7,
    PackedBones = 8,
    SceneDesc = 9,
    TextureTable = 10,
    TextureCount = 11,

    /// <summary>
    /// Carried through unread. Present in retail files. What the loader does with it is
    /// not known.
    /// </summary>
    Unknown12 = 12,

    /// <summary>
    /// Carried through unread, as <see cref="Unknown12"/>.
    /// </summary>
    Unknown13 = 13,
}
