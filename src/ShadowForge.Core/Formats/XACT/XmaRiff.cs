namespace ShadowForge.Formats.XACT;

/// <summary>
/// Wraps a wave bank's XMA entry in a RIFF WAVE file with an XMA2 fmt chunk
/// (WAVE_FORMAT_XMA2, 0x166), the form general-purpose decoders such as FFmpeg read. The
/// packets are written as the bank holds them, in one 64 KiB-block stream per channel pair;
/// no seek chunk is needed for a front-to-back decode.
/// </summary>
public static class XmaRiff
{
    public const ushort FormatXma2 = 0x166;
    private const int BlockSize = 0x10000;
    private const int Xma2ExtraSize = 34;

    public static byte[] Wrap(WaveBankEntry entry)
    {
        if (entry.Format.Tag != WaveFormatTag.XMA)
            throw new ArgumentException($"The entry is {entry.Format.Tag}, not XMA.", nameof(entry));

        int channels = entry.Format.Channels;
        int rate = entry.Format.SamplesPerSecond;
        byte[] data = entry.Data;

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int fmtSize = 18 + Xma2ExtraSize;
        w.Write("RIFF"u8);
        w.Write(4 + 8 + fmtSize + 8 + data.Length);
        w.Write("WAVE"u8);

        w.Write("fmt "u8);
        w.Write(fmtSize);
        w.Write(FormatXma2);
        w.Write((ushort)channels);
        w.Write((uint)rate);
        w.Write((uint)(rate * channels * 2));
        w.Write((ushort)(channels * 2));
        w.Write((ushort)16);
        w.Write((ushort)Xma2ExtraSize);
        w.Write((ushort)((channels + 1) / 2));
        w.Write(ChannelMask(channels));
        w.Write(entry.DurationSamples);
        w.Write((uint)BlockSize);
        w.Write(0u);
        w.Write(entry.DurationSamples);
        w.Write(0u);
        w.Write(0u);
        w.Write((byte)0);
        w.Write((byte)4);
        w.Write((ushort)((data.Length + BlockSize - 1) / BlockSize));

        w.Write("data"u8);
        w.Write(data.Length);
        w.Write(data);
        return ms.ToArray();
    }

    /// <summary>
    /// Front centre for mono, front left and right for stereo, and the first channels in
    /// speaker order otherwise.
    /// </summary>
    private static uint ChannelMask(int channels) => channels switch
    {
        1 => 0x4,
        2 => 0x3,
        _ => (1u << channels) - 1,
    };
}
