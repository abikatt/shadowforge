namespace ShadowForge.Formats.XACT;

/// <summary>
/// The packed mini wave format dword of a wave bank entry: tag in bits 0-1, channel count in
/// bits 2-4, sample rate in bits 5-22, block align in bits 23-30 and sample width in bit 31
/// (0 = 8-bit, 1 = 16-bit). Retail example: 0x81177004 is PCM, 1 channel, 48000 Hz, block
/// align 2, 16-bit.
/// </summary>
public readonly record struct WaveFormat(uint Packed)
{
    public const int TagShift = 0;
    public const uint TagMask = 0x3;
    public const int ChannelsShift = 2;
    public const uint ChannelsMask = 0x7;
    public const int SamplesPerSecondShift = 5;
    public const uint SamplesPerSecondMask = 0x3FFFF;
    public const int BlockAlignShift = 23;
    public const uint BlockAlignMask = 0xFF;
    public const int BitsPerSampleShift = 31;

    public WaveFormatTag Tag => (WaveFormatTag)((Packed >> TagShift) & TagMask);
    public int Channels => (int)((Packed >> ChannelsShift) & ChannelsMask);
    public int SamplesPerSecond => (int)((Packed >> SamplesPerSecondShift) & SamplesPerSecondMask);
    public int BlockAlign => (int)((Packed >> BlockAlignShift) & BlockAlignMask);
    public int BitsPerSample => ((Packed >> BitsPerSampleShift) & 1) != 0 ? 16 : 8;

    /// <summary>
    /// Throws when a field does not fit its bit range or the width is not 8 or 16.
    /// </summary>
    public static WaveFormat Pack(WaveFormatTag tag, int channels, int samplesPerSecond, int blockAlign, int bitsPerSample)
    {
        if (channels < 0 || channels > ChannelsMask)
            throw new ArgumentOutOfRangeException(nameof(channels), channels,
                $"Channel count must fit in 3 bits (0-{ChannelsMask}).");
        if (samplesPerSecond < 0 || samplesPerSecond > SamplesPerSecondMask)
            throw new ArgumentOutOfRangeException(nameof(samplesPerSecond), samplesPerSecond,
                $"Sample rate must fit in 18 bits (0-{SamplesPerSecondMask}).");
        if (blockAlign < 0 || blockAlign > BlockAlignMask)
            throw new ArgumentOutOfRangeException(nameof(blockAlign), blockAlign,
                $"Block align must fit in 8 bits (0-{BlockAlignMask}).");
        if (bitsPerSample is not (8 or 16))
            throw new ArgumentOutOfRangeException(nameof(bitsPerSample), bitsPerSample,
                "Only 8-bit and 16-bit sample widths are representable.");

        uint packed = (uint)tag & TagMask;
        packed |= ((uint)channels & ChannelsMask) << ChannelsShift;
        packed |= ((uint)samplesPerSecond & SamplesPerSecondMask) << SamplesPerSecondShift;
        packed |= ((uint)blockAlign & BlockAlignMask) << BlockAlignShift;
        if (bitsPerSample == 16) packed |= 1u << BitsPerSampleShift;
        return new WaveFormat(packed);
    }

    /// <summary>
    /// 16-bit PCM, whose block align is two bytes per channel.
    /// </summary>
    public static WaveFormat Pcm16(int channels, int samplesPerSecond)
        => Pack(WaveFormatTag.PCM, channels, samplesPerSecond, channels * 2, 16);

    public override string ToString()
        => $"{Tag} {Channels}ch {SamplesPerSecond}Hz {BitsPerSample}-bit align {BlockAlign}";
}
