namespace ShadowForge.Formats.HMB;

/// <summary>
/// Exactly one of <see cref="Linear"/> and <see cref="Axes"/> is set. Axes holds one angle
/// curve per component, X, Y then Z.
/// </summary>
public sealed class EulerChannel
{
    public LinearEulerKey[]? Linear;
    public HermiteCurve[]? Axes;
}
