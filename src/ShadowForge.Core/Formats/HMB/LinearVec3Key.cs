using System.Numerics;

namespace ShadowForge.Formats.HMB;

/// <summary>
/// 16 bytes: s16 frame, two zero bytes, then three floats.
/// </summary>
public sealed record LinearVec3Key(short Frame, Vector3 Value)
{
    public const int Size = 16;
}
