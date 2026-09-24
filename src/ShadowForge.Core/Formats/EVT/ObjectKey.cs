namespace ShadowForge.Formats.EVT;

/// <summary>
/// Key types of an object track. An actor track (see <see cref="EventTrack.IsActor"/>)
/// shares Show, Hide, Motion and MotionAt but reads every other key type differently.
/// </summary>
public enum ObjectKey
{
    Show = 1,
    Hide = 2,
    Position = 3,
    Rotation = 4,
    Scale = 5,
    Motion = 6,
    PositionLerp = 7,
    RotationLerp = 8,
    ScaleLerp = 9,
    Attach = 11,
    MovePath = 12,
    GuidePath = 13,
    MotionAt = 22,
    Color = 24,
    ColorLerp = 25,
    Transform = 27,
    AttachRotated = 28,
    MeshParts = 29,
    MaterialColor = 39,
}
