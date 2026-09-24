namespace ShadowForge.Formats.HMB;

public sealed class MotionClip
{
    /// <summary>
    /// Playback rate used when <see cref="RateFlag"/> is not positive.
    /// </summary>
    public const float DefaultFrameRate = 30f;

    public string Name = "";

    /// <summary>
    /// 2 stores every channel as linear keys. 3 stores hermite curves, except channels with
    /// a single key, which stay linear.
    /// </summary>
    public int Mode;

    public int DurationFrames;
    public float Rate;
    public float RateFlag;
    public float EffectiveRate => RateFlag > 0f ? Rate : DefaultFrameRate;

    public int FrameDivisor => RateFlag > 0f ? Math.Max(1, (int)(Rate / DefaultFrameRate)) : 1;

    public List<MotionTrack> Tracks = new();

    /// <summary>
    /// First-table section types other than <see cref="MotionSection"/>, kept but not interpreted.
    /// </summary>
    public List<int> ExtraSectionTypes = new();
}
