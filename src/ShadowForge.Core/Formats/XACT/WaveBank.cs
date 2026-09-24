using ShadowForge.IO;

namespace ShadowForge.Formats.XACT;

/// <summary>
/// An Xbox 360 XACT wave bank (.xwb). The file is big-endian and its signature reads "DNBW"
/// ("WBND" byte-reversed). The bank data, seek table and entry name segments are kept
/// verbatim, so a rebuilt bank differs from the original only where a wave was replaced.
/// </summary>
public sealed class WaveBank
{
    public static ReadOnlySpan<byte> Signature => [0x44, 0x4E, 0x42, 0x57];

    /// <summary>Signature, version and the five segment records.</summary>
    public const int HeaderSize = 0x30;

    public const int VersionOffset = 0x04;
    public const int SegmentTableOffset = 0x08;
    public const int SegmentCount = 5;

    public const int SegmentBankData = 0;
    public const int SegmentEntryMetaData = 1;
    public const int SegmentSeekTables = 2;
    public const int SegmentEntryNames = 3;
    public const int SegmentEntryWaveData = 4;

    /// <summary>Size of the BANKDATA segment in retail Blue Dragon banks.</summary>
    public const int BankDataSize = 0x60;

    public const int BankFlagsOffset = 0x00;
    public const int BankEntryCountOffset = 0x04;
    public const int BankNameOffset = 0x08;
    public const int BankNameFieldWidth = 64;
    public const int BankEntryMetaDataElementSizeOffset = 0x48;
    public const int BankEntryNameElementSizeOffset = 0x4C;
    public const int BankAlignmentOffset = 0x50;
    public const int BankCompactFormatOffset = 0x54;
    public const int BankBuildTimeOffset = 0x58;

    public const uint DefaultAlignment = 2048;

    /// <summary>Bank format version, 40 in retail Blue Dragon banks.</summary>
    public uint Version { get; set; } = 40;

    /// <summary>The BANKDATA segment verbatim. The named bank fields below read from it.</summary>
    public byte[] BankData { get; set; } = new byte[BankDataSize];

    /// <summary>
    /// The SEEKTABLES segment verbatim. Only XMA entries use it, so a PCM replacement leaves
    /// it unchanged.
    /// </summary>
    public byte[] SeekTables { get; set; } = [];

    /// <summary>The ENTRYNAMES segment verbatim, empty in retail Blue Dragon banks.</summary>
    public byte[] EntryNames { get; set; } = [];

    public List<WaveBankEntry> Entries { get; } = [];

    /// <summary>
    /// The file offsets the segments were read from. A rebuild keeps all five, so only the
    /// ENTRYWAVEDATA length changes.
    /// </summary>
    public uint[] SegmentOffsets { get; set; } = new uint[SegmentCount];

    public uint Flags => BigEndian.ReadUInt32(BankData, BankFlagsOffset);

    public string Name => BankData.DecodeASCII(BankNameOffset, BankNameFieldWidth);

    public uint EntryMetaDataElementSize
        => BigEndian.ReadUInt32(BankData, BankEntryMetaDataElementSizeOffset);

    public uint EntryNameElementSize
        => BigEndian.ReadUInt32(BankData, BankEntryNameElementSizeOffset);

    public uint CompactFormat => BigEndian.ReadUInt32(BankData, BankCompactFormatOffset);

    /// <summary>
    /// Wave data is padded to a multiple of this, 2048 in retail banks. A stored zero reads
    /// as <see cref="DefaultAlignment"/> because a rebuild cannot pad to zero.
    /// </summary>
    public uint Alignment
    {
        get
        {
            uint value = BigEndian.ReadUInt32(BankData, BankAlignmentOffset);
            return value == 0 ? DefaultAlignment : value;
        }
    }

    /// <summary>
    /// Captures all five segments, decodes the entry records and copies each entry's wave
    /// data out without padding.
    /// </summary>
    public static WaveBank Read(byte[] data, string name)
    {
        if (data.Length < HeaderSize || !data.AsSpan(0, 4).SequenceEqual(Signature))
            throw new InvalidDataException($"{name}: not an Xbox 360 XACT wave bank (WBND)");

        var bank = new WaveBank { Version = BigEndian.ReadUInt32(data, VersionOffset) };

        var lengths = new uint[SegmentCount];
        for (int i = 0; i < SegmentCount; i++)
        {
            bank.SegmentOffsets[i] = BigEndian.ReadUInt32(data, SegmentTableOffset + i * 8);
            lengths[i] = BigEndian.ReadUInt32(data, SegmentTableOffset + i * 8 + 4);
        }

        bank.BankData = Segment(data, bank.SegmentOffsets[SegmentBankData],
                                lengths[SegmentBankData], name, "BANKDATA");
        if (bank.BankData.Length < BankDataSize)
            throw new InvalidDataException(
                $"{name}: BANKDATA segment is {bank.BankData.Length} bytes, expected at least {BankDataSize}");

        bank.SeekTables = Segment(data, bank.SegmentOffsets[SegmentSeekTables],
                                  lengths[SegmentSeekTables], name, "SEEKTABLES");
        bank.EntryNames = Segment(data, bank.SegmentOffsets[SegmentEntryNames],
                                  lengths[SegmentEntryNames], name, "ENTRYNAMES");

        uint elementSize = bank.EntryMetaDataElementSize;
        if (elementSize != WaveBankEntry.RecordSize)
            throw new InvalidDataException(
                $"{name}: entry metadata element size {elementSize} is unsupported " +
                $"(only the {WaveBankEntry.RecordSize}-byte record is handled, not compact banks).");

        int entryCount = (int)BigEndian.ReadUInt32(bank.BankData, BankEntryCountOffset);
        uint metaOffset = bank.SegmentOffsets[SegmentEntryMetaData];
        uint waveDataOffset = bank.SegmentOffsets[SegmentEntryWaveData];

        for (int i = 0; i < entryCount; i++)
        {
            long record = metaOffset + (long)i * elementSize;
            if (record + WaveBankEntry.RecordSize > data.Length)
                throw new InvalidDataException($"{name}: entry {i} metadata runs past the end of the file");

            uint flagsAndDuration = BigEndian.ReadUInt32(data, (int)(record + WaveBankEntry.FlagsAndDurationOffset));
            uint playOffset = BigEndian.ReadUInt32(data, (int)(record + WaveBankEntry.PlayRegionOffsetOffset));
            uint playLength = BigEndian.ReadUInt32(data, (int)(record + WaveBankEntry.PlayRegionLengthOffset));

            long start = (long)waveDataOffset + playOffset;
            if (start + playLength > data.Length)
                throw new InvalidDataException(
                    $"{name}: entry {i} wave data (offset {playOffset}, {playLength} bytes) runs past the end of the file");

            var entry = new WaveBankEntry
            {
                Flags = flagsAndDuration & 0xF,
                DurationSamples = flagsAndDuration >> 4,
                Format = new WaveFormat(BigEndian.ReadUInt32(data, (int)(record + WaveBankEntry.FormatOffset))),
                LoopRegionStartSample = BigEndian.ReadUInt32(data, (int)(record + WaveBankEntry.LoopRegionStartOffset)),
                LoopRegionTotalSamples = BigEndian.ReadUInt32(data, (int)(record + WaveBankEntry.LoopRegionTotalOffset)),
                Data = new byte[playLength],
            };
            Array.Copy(data, (int)start, entry.Data, 0, (int)playLength);
            bank.Entries.Add(entry);
        }

        return bank;
    }

    public static WaveBank ReadFile(string path)
        => Read(File.ReadAllBytes(path), Path.GetFileName(path));

    /// <summary>
    /// Points an entry at new 16-bit PCM audio, byte-swapping the samples to the bank's
    /// big-endian order. Flags and the loop region are cleared, as in retail PCM entries.
    /// </summary>
    public void ReplaceEntry(int index, WavFile wav)
    {
        if (index < 0 || index >= Entries.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index,
                $"Wave index must be 0-{Entries.Count - 1}.");

        var entry = Entries[index];
        entry.Format = WaveFormat.Pcm16(wav.Channels, wav.SampleRate);
        entry.Data = WavFile.SwapSamples(wav.Samples);
        entry.Flags = 0;
        entry.DurationSamples = (uint)wav.FrameCount;
        entry.LoopRegionStartSample = 0;
        entry.LoopRegionTotalSamples = 0;
    }

    /// <summary>
    /// Lays the play regions out again as a running offset padded to <see cref="Alignment"/>.
    /// Every segment other than the entry metadata and the wave data is written back verbatim
    /// at its original offset.
    /// </summary>
    public byte[] Write()
    {
        uint alignment = Alignment;
        uint waveDataOffset = SegmentOffsets[SegmentEntryWaveData];

        var playOffsets = new uint[Entries.Count];
        long running = 0;
        for (int i = 0; i < Entries.Count; i++)
        {
            playOffsets[i] = checked((uint)running);
            running += Align.Up(Entries[i].Data.Length, (long)alignment);
        }
        uint waveDataLength = checked((uint)running);

        var output = new byte[checked(waveDataOffset + waveDataLength)];

        Signature.CopyTo(output.AsSpan(0, 4));
        BigEndian.WriteUInt32(output, VersionOffset, Version);

        var lengths = new uint[SegmentCount];
        lengths[SegmentBankData] = (uint)BankData.Length;
        lengths[SegmentEntryMetaData] = (uint)(Entries.Count * WaveBankEntry.RecordSize);
        lengths[SegmentSeekTables] = (uint)SeekTables.Length;
        lengths[SegmentEntryNames] = (uint)EntryNames.Length;
        lengths[SegmentEntryWaveData] = waveDataLength;

        for (int i = 0; i < SegmentCount; i++)
        {
            BigEndian.WriteUInt32(output, SegmentTableOffset + i * 8, SegmentOffsets[i]);
            BigEndian.WriteUInt32(output, SegmentTableOffset + i * 8 + 4, lengths[i]);
        }

        CopySegment(output, SegmentOffsets[SegmentBankData], BankData);
        CopySegment(output, SegmentOffsets[SegmentSeekTables], SeekTables);
        CopySegment(output, SegmentOffsets[SegmentEntryNames], EntryNames);

        uint metaOffset = SegmentOffsets[SegmentEntryMetaData];
        for (int i = 0; i < Entries.Count; i++)
        {
            var entry = Entries[i];
            int record = (int)(metaOffset + i * WaveBankEntry.RecordSize);
            BigEndian.WriteUInt32(output, record + WaveBankEntry.FlagsAndDurationOffset, entry.FlagsAndDuration);
            BigEndian.WriteUInt32(output, record + WaveBankEntry.FormatOffset, entry.Format.Packed);
            BigEndian.WriteUInt32(output, record + WaveBankEntry.PlayRegionOffsetOffset, playOffsets[i]);
            BigEndian.WriteUInt32(output, record + WaveBankEntry.PlayRegionLengthOffset, (uint)entry.Data.Length);
            BigEndian.WriteUInt32(output, record + WaveBankEntry.LoopRegionStartOffset, entry.LoopRegionStartSample);
            BigEndian.WriteUInt32(output, record + WaveBankEntry.LoopRegionTotalOffset, entry.LoopRegionTotalSamples);

            Array.Copy(entry.Data, 0, output, waveDataOffset + playOffsets[i], entry.Data.Length);
        }

        return output;
    }

    public void WriteFile(string path) => File.WriteAllBytes(path, Write());

    /// <summary>
    /// An empty segment is skipped, so its recorded offset may lie past the end of the output.
    /// </summary>
    private static void CopySegment(byte[] output, uint offset, byte[] segment)
    {
        if (segment.Length == 0) return;
        Array.Copy(segment, 0, output, offset, segment.Length);
    }

    private static byte[] Segment(byte[] data, uint offset, uint length, string name, string label)
    {
        if (length == 0) return [];
        if (offset + (long)length > data.Length)
            throw new InvalidDataException(
                $"{name}: {label} segment (offset {offset}, {length} bytes) runs past the end of the file");
        var segment = new byte[length];
        Array.Copy(data, (int)offset, segment, 0, (int)length);
        return segment;
    }
}
