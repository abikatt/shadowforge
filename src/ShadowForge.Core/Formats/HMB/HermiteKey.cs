namespace ShadowForge.Formats.HMB;

/// <summary>
/// 8 bytes: s16 frame, u16 value, f32 outgoing tangent per second. The value is a half float,
/// or a <see cref="RawAngle"/> step on an angle curve, where the tangent is in steps too.
/// </summary>
public sealed record HermiteKey(short Frame, ushort RawValue, float OutTangent)
{
    public const int Size = 8;
}
