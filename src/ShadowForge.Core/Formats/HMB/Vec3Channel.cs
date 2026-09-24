namespace ShadowForge.Formats.HMB;

/// <summary>
/// Exactly one of <see cref="Linear"/> and <see cref="Axes"/> is set. Axes holds one curve
/// per component, X, Y then Z.
/// </summary>
public sealed class Vec3Channel
{
    public LinearVec3Key[]? Linear;
    public HermiteCurve[]? Axes;
}
