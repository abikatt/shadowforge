namespace ShadowForge.Formats.HMB;

/// <summary>
/// 8 bytes: s16 frame, then X, Y and Z as <see cref="RawAngle"/> steps.
/// </summary>
public sealed record LinearEulerKey(short Frame, short X, short Y, short Z)
{
    public const int Size = 8;
}
