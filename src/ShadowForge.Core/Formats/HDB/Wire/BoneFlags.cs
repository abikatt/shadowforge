namespace ShadowForge.Formats.HDB.Wire;

/// <summary>
/// Bits in a bone record's flags field. Each transform bit tells the loader
/// that the matching field carries a non-identity value and must be composed
/// into the bone's local transform.
/// </summary>
[Flags]
public enum BoneFlags : uint
{
    None = 0,
    Position = 0x01,
    Euler = 0x04,
    Scale = 0x08,
    ExtraEulerA = 0x10,
    ExtraEulerB = 0x20,

    /// <summary>
    /// Set on a root bone, which the runtime anchors rather than parenting.
    /// </summary>
    Anchor = 0x40,

    /// <summary>
    /// Set on the bone whose ModelPtr field points at the type-6 model
    /// record, i.e. the bone the geometry hangs from.
    /// </summary>
    Model = 0x10000,

    /// <summary>
    /// Set by the importer on every bone that has a parent, in place of
    /// <see cref="Anchor"/>.
    /// </summary>
    Parented = 0x100000,
}
