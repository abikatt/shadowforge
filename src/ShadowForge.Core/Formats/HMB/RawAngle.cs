namespace ShadowForge.Formats.HMB;

/// <summary>
/// The fixed-point angle of euler keys and hermite angle curves: a signed 16-bit step worth
/// <see cref="Scale"/> radians, so a full turn is exactly 0x10000 steps. The shortest-path
/// unwrap in the sampler and the importer depends on that.
/// </summary>
public static class RawAngle
{
    public const float Scale = 0.000095873802f;

    public static float ToRadians(float raw) => raw * Scale;

    public static short FromRadians(float radians)
        => (short)(((int)MathF.Round(radians / Scale)) & 0xFFFF);
}
