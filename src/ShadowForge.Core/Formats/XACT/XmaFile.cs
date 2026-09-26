using System.Buffers.Binary;
using System.Text;
using ShadowForge.IO;

namespace ShadowForge.Formats.XACT;

/// <summary>
/// An XMA2 file as the Xbox 360 SDK's xmaencode writes it for XACT (xmaencode in.wav /S): a
/// RIFF WAVE whose "data" chunk holds the XMA packets in 64 KiB blocks, an "XMA2" chunk
/// (XMA2WAVEFORMAT, big-endian) with the sample counts and loop, and a "seek" chunk with the
/// running sample count at the end of each block (big-endian). The packets and seek table are
/// what a wave bank entry stores, so the file drops into a bank unchanged. Only single-stream
/// files (mono or stereo) are read, since a wave bank entry has one stream.
/// </summary>
public sealed class XmaFile
{
    public const int Xma2ChunkMinSize = 44;

    public int Channels { get; init; }

    public int SampleRate { get; init; }

    /// <summary>The XMA packets, as a bank entry's play region holds them.</summary>
    public byte[] Data { get; init; } = [];

    /// <summary>
    /// Samples per channel the packets decode to, including the encoder's padding. A bank
    /// entry's duration.
    /// </summary>
    public uint SamplesEncoded { get; init; }

    public uint LoopBegin { get; init; }

    /// <summary>The loop's length in samples, 0 when the file does not loop.</summary>
    public uint LoopLength { get; init; }

    /// <summary>The running sample count at the end of each block.</summary>
    public uint[] SeekTable { get; init; } = [];

    public static XmaFile Read(byte[] data, string name)
    {
        if (data.Length < 12 ||
            Encoding.ASCII.GetString(data, 0, 4) != "RIFF" ||
            Encoding.ASCII.GetString(data, 8, 4) != "WAVE")
            throw new InvalidDataException($"{name}: not a RIFF WAVE file");

        byte[]? packets = null, xma2 = null, seek = null;
        int cursor = 12;
        while (cursor + 8 <= data.Length)
        {
            string id = Encoding.ASCII.GetString(data, cursor, 4);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(cursor + 4, 4));
            int body = cursor + 8;
            if (body + (long)size > data.Length)
                throw new InvalidDataException($"{name}: chunk '{id}' claims {size} bytes but the file ends first");

            byte[] chunk = data.AsSpan(body, (int)size).ToArray();
            switch (id)
            {
                case "data": packets = chunk; break;
                case "XMA2": xma2 = chunk; break;
                case "seek": seek = chunk; break;
            }
            cursor = body + (int)size + (int)(size & 1);
        }

        if (xma2 is null)
            throw new InvalidDataException(
                $"{name}: no XMA2 chunk. Encode with xmaencode's /S option, without /P.");
        if (xma2.Length < Xma2ChunkMinSize)
            throw new InvalidDataException($"{name}: XMA2 chunk is {xma2.Length} bytes, expected at least {Xma2ChunkMinSize}");
        if (packets is null) throw new InvalidDataException($"{name}: no data chunk");
        if (seek is null) throw new InvalidDataException($"{name}: no seek chunk");

        int streams = xma2[1];
        if (streams != 1)
            throw new InvalidDataException(
                $"{name}: {streams} XMA streams. Only mono and stereo waves, one stream, fit a wave bank entry.");
        uint loopBegin = BigEndian.ReadUInt32(xma2, 4);
        uint loopEnd = BigEndian.ReadUInt32(xma2, 8);
        uint blockCount = BigEndian.ReadUInt32(xma2, 36);

        var table = new uint[seek.Length / 4];
        for (int i = 0; i < table.Length; i++) table[i] = BigEndian.ReadUInt32(seek, i * 4);
        if (table.Length != blockCount)
            throw new InvalidDataException($"{name}: the seek chunk has {table.Length} entries for {blockCount} blocks");

        return new XmaFile
        {
            Channels = xma2[40],
            SampleRate = (int)BigEndian.ReadUInt32(xma2, 12),
            Data = packets,
            SamplesEncoded = BigEndian.ReadUInt32(xma2, 28),
            LoopBegin = xma2[3] == 0 ? 0 : loopBegin,
            LoopLength = xma2[3] == 0 || loopEnd < loopBegin ? 0 : loopEnd - loopBegin,
            SeekTable = table,
        };
    }

    public static XmaFile ReadFile(string path) => Read(File.ReadAllBytes(path), Path.GetFileName(path));

    /// <summary>
    /// The mini format retail XMA entries carry: 16-bit, block align two bytes per channel.
    /// </summary>
    public WaveFormat Format => WaveFormat.Pack(WaveFormatTag.XMA, Channels, SampleRate, Channels * 2, 16);

    /// <summary>
    /// A bank entry holding this wave, looping where the file loops, as retail entries do.
    /// </summary>
    public WaveBankEntry ToEntry() => new()
    {
        Format = Format,
        Data = Data,
        DurationSamples = SamplesEncoded,
        LoopRegionStartSample = LoopBegin,
        LoopRegionTotalSamples = LoopLength,
    };
}
