namespace ShadowForge.Formats.HMB;

/// <summary>
/// A 36-byte record: self-relative s32 offsets to the T, R and S key data (0 when absent),
/// u16 key counts in the same order, then the bone name.
/// </summary>
public sealed class MotionTrack
{
    public const int RecordSize = 36;

    public const int TranslationDataField = 0;
    public const int RotationDataField = 4;
    public const int ScaleDataField = 8;
    public const int TranslationCountField = 12;
    public const int RotationCountField = 14;
    public const int ScaleCountField = 16;
    public const int NameField = 18;

    /// <summary>
    /// Width of the NUL-padded bone name field, so a name holds at most 17 characters.
    /// </summary>
    public const int NameFieldSize = 18;

    public string BoneName = "";
    public Vec3Channel? Translation;
    public EulerChannel? Rotation;
    public Vec3Channel? Scale;
}
