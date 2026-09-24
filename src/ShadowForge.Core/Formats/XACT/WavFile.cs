using System.Buffers.Binary;
using System.Text;
using ShadowForge.IO;

namespace ShadowForge.Formats.XACT;

/// <summary>
/// A 16-bit PCM RIFF WAV, the interchange format for waves pulled out of or pushed into a
/// bank. RIFF samples are little-endian while an Xbox 360 wave bank stores PCM big-endian.
/// <see cref="SwapSamples"/> converts between the two.
/// </summary>
public sealed class WavFile
{
    /// <summary>The canonical RIFF, fmt and data chunk headers.</summary>
    public const int HeaderSize = 44;

    /// <summary>WAVE_FORMAT_PCM, the only fmt tag read or written.</summary>
    public const ushort FormatPcm = 1;

    public const int BitsPerSample = 16;

    public int Channels { get; set; } = 1;

    public int SampleRate { get; set; } = 48000;

    /// <summary>Interleaved little-endian 16-bit samples.</summary>
    public byte[] Samples { get; set; } = [];

    /// <summary>Sample frames, that is samples per channel.</summary>
    public int FrameCount => Channels == 0 ? 0 : Samples.Length / (2 * Channels);

    /// <summary>
    /// Walks the chunk list for "fmt " and "data", skipping other chunks. Only 16-bit PCM is accepted.
    /// </summary>
    public static WavFile Read(byte[] data, string name)
    {
        if (data.Length < 12 ||
            Encoding.ASCII.GetString(data, 0, 4) != "RIFF" ||
            Encoding.ASCII.GetString(data, 8, 4) != "WAVE")
            throw new InvalidDataException($"{name}: not a RIFF WAVE file");

        var wav = new WavFile();
        bool haveFormat = false;
        bool haveData = false;

        int cursor = 12;
        while (cursor + 8 <= data.Length)
        {
            string id = Encoding.ASCII.GetString(data, cursor, 4);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(cursor + 4, 4));
            int body = cursor + 8;
            if (body + (long)size > data.Length)
                throw new InvalidDataException($"{name}: chunk '{id}' claims {size} bytes but the file ends first");

            if (id == "fmt ")
            {
                if (size < 16)
                    throw new InvalidDataException($"{name}: fmt chunk is {size} bytes, expected at least 16");
                ushort tag = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(body, 2));
                if (tag != FormatPcm)
                    throw new InvalidDataException(
                        $"{name}: fmt tag {tag} is not PCM, only uncompressed 16-bit PCM is supported");
                wav.Channels = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(body + 2, 2));
                wav.SampleRate = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(body + 4, 4));
                int bits = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(body + 14, 2));
                if (bits != BitsPerSample)
                    throw new InvalidDataException(
                        $"{name}: {bits}-bit samples are not supported, convert to {BitsPerSample}-bit PCM first");
                haveFormat = true;
            }
            else if (id == "data")
            {
                wav.Samples = new byte[size];
                Array.Copy(data, body, wav.Samples, 0, (int)size);
                haveData = true;
            }

            cursor = body + (int)size;
            if ((size & 1) != 0) cursor++;
        }

        if (!haveFormat) throw new InvalidDataException($"{name}: no fmt chunk");
        if (!haveData) throw new InvalidDataException($"{name}: no data chunk");
        if (wav.Channels < 1)
            throw new InvalidDataException($"{name}: fmt chunk declares {wav.Channels} channels");
        return wav;
    }

    public static WavFile ReadFile(string path)
        => Read(File.ReadAllBytes(path), Path.GetFileName(path));

    public static WavFile FromBigEndianPcm(byte[] data, int channels, int sampleRate)
        => new() { Channels = channels, SampleRate = sampleRate, Samples = SwapSamples(data) };

    public byte[] Write()
    {
        int blockAlign = Channels * 2;
        var output = new byte[HeaderSize + Samples.Length];

        Encoding.ASCII.GetBytes("RIFF").CopyTo(output, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(4, 4), (uint)(36 + Samples.Length));
        Encoding.ASCII.GetBytes("WAVE").CopyTo(output, 8);

        Encoding.ASCII.GetBytes("fmt ").CopyTo(output, 12);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(16, 4), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(20, 2), FormatPcm);
        BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(22, 2), (ushort)Channels);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(24, 4), (uint)SampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(28, 4), (uint)(SampleRate * blockAlign));
        BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(32, 2), (ushort)blockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(34, 2), BitsPerSample);

        Encoding.ASCII.GetBytes("data").CopyTo(output, 36);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(40, 4), (uint)Samples.Length);
        Samples.CopyTo(output, HeaderSize);

        return output;
    }

    public void WriteFile(string path) => File.WriteAllBytes(path, Write());

    /// <summary>
    /// A copy with every 16-bit sample byte-swapped. A trailing odd byte is copied as is.
    /// </summary>
    public static byte[] SwapSamples(byte[] data)
    {
        var swapped = (byte[])data.Clone();
        ByteSwap.Swap16(swapped);
        return swapped;
    }
}
