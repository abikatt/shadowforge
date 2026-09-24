namespace ShadowForge.Formats.HMB;

/// <summary>
/// First-table section types the reader interprets. Any other type is recorded on
/// <see cref="MotionClip.ExtraSectionTypes"/>.
/// </summary>
public enum MotionSection
{
    Relocated = 0x103,
    Motion = 0x105,
}
