namespace ShadowForge.Formats.XACT;

/// <summary>
/// One wave inside a bank. PCM data is held big-endian, as the bank stores it.
/// </summary>
public sealed class WaveBankEntry
{
    /// <summary>
    /// Byte size of the entry record, and the entryMetaDataElementSize of every retail
    /// Blue Dragon bank.
    /// </summary>
    public const int RecordSize = 24;

    public const int FlagsAndDurationOffset = 0x00;
    public const int FormatOffset = 0x04;
    public const int PlayRegionOffsetOffset = 0x08;
    public const int PlayRegionLengthOffset = 0x0C;
    public const int LoopRegionStartOffset = 0x10;
    public const int LoopRegionTotalOffset = 0x14;

    /// <summary>
    /// The duration is the upper 28 bits of the flags-and-duration word.
    /// </summary>
    public const uint MaxDurationSamples = 0x0FFFFFFF;

    /// <summary>Low four bits of the flags-and-duration word.</summary>
    public uint Flags { get; set; }

    /// <summary>Samples per channel.</summary>
    public uint DurationSamples { get; set; }

    public WaveFormat Format { get; set; }

    public uint LoopRegionStartSample { get; set; }

    public uint LoopRegionTotalSamples { get; set; }

    /// <summary>
    /// The wave data without padding. Its length is the record's playRegionLength. The bank
    /// pads the stored copy up to the alignment.
    /// </summary>
    public byte[] Data { get; set; } = [];

    /// <summary>
    /// The flags-and-duration word as the record stores it. Throws when the duration does not fit.
    /// </summary>
    public uint FlagsAndDuration
    {
        get
        {
            if (DurationSamples > MaxDurationSamples)
                throw new InvalidDataException(
                    $"Duration {DurationSamples} exceeds the 28-bit field limit of {MaxDurationSamples} samples.");
            return (DurationSamples << 4) | (Flags & 0xF);
        }
    }
}
