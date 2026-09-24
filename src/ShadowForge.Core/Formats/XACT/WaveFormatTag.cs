namespace ShadowForge.Formats.XACT;

/// <summary>
/// The codec of a wave bank entry, bits 0-1 of <see cref="WaveFormat.Packed"/>.
/// </summary>
public enum WaveFormatTag
{
    PCM = 0,
    XMA = 1,
    ADPCM = 2,
    WMA = 3,
}
