using System.Buffers.Binary;
using ShadowForge.Formats.XACT;
using ShadowForge.GameData.Audio;

namespace ShadowForge.Tests.Formats.XACT;

public sealed class XmaRiffTests
{
    private static WaveBankEntry XmaEntry(int channels, int bytes, uint samples) => new()
    {
        Format = WaveFormat.Pack(WaveFormatTag.XMA, channels, 48000, 4, 16),
        Data = Enumerable.Range(0, bytes).Select(i => (byte)i).ToArray(),
        DurationSamples = samples,
    };

    [Fact]
    public void Wrap_Stereo_WritesXma2FmtAndTheDataVerbatim()
    {
        var entry = XmaEntry(2, 0x10000 * 2 + 5, 123456);

        byte[] riff = XmaRiff.Wrap(entry);
        var span = riff.AsSpan();

        Assert.Equal("RIFF"u8.ToArray(), span[..4].ToArray());
        Assert.Equal(riff.Length - 8, BinaryPrimitives.ReadInt32LittleEndian(span[4..]));
        Assert.Equal("WAVEfmt "u8.ToArray(), span[8..16].ToArray());
        Assert.Equal(52, BinaryPrimitives.ReadInt32LittleEndian(span[16..]));
        var fmt = span[20..];
        Assert.Equal(XmaRiff.FormatXma2, BinaryPrimitives.ReadUInt16LittleEndian(fmt));
        Assert.Equal(2, BinaryPrimitives.ReadUInt16LittleEndian(fmt[2..]));
        Assert.Equal(48000u, BinaryPrimitives.ReadUInt32LittleEndian(fmt[4..]));
        Assert.Equal(34, BinaryPrimitives.ReadUInt16LittleEndian(fmt[16..]));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(fmt[18..]));
        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(fmt[20..]));
        Assert.Equal(123456u, BinaryPrimitives.ReadUInt32LittleEndian(fmt[24..]));
        Assert.Equal(3, BinaryPrimitives.ReadUInt16LittleEndian(fmt[50..]));

        var data = span[(20 + 52)..];
        Assert.Equal("data"u8.ToArray(), data[..4].ToArray());
        Assert.Equal(entry.Data.Length, BinaryPrimitives.ReadInt32LittleEndian(data[4..]));
        Assert.True(data[8..].SequenceEqual(entry.Data));
    }

    [Fact]
    public void Wrap_Mono_UsesOneStreamAndTheCentreChannel()
    {
        var fmt = XmaRiff.Wrap(XmaEntry(1, 100, 10)).AsSpan(20);

        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(fmt[2..]));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(fmt[18..]));
        Assert.Equal(4u, BinaryPrimitives.ReadUInt32LittleEndian(fmt[20..]));
    }

    [Fact]
    public void Wrap_PcmEntry_Throws() =>
        Assert.Throws<ArgumentException>(() => XmaRiff.Wrap(new WaveBankEntry { Format = WaveFormat.Pcm16(1, 48000) }));

    [SkippableFact]
    public void Catalog_RetailBanks_ListWavesWithCueNames()
    {
        var banks = new SoundBankCatalog(RetailData.Install).List();
        Skip.If(banks.Count == 0, "no snd_* folders in the located game data");

        var bank = banks.FirstOrDefault(b => b.Folder == "snd_memory" && b.Name == "ae08");
        Skip.If(bank is null, "snd_memory\\se\\ae08.xwb is not in the located game data");
        Assert.Equal(@"snd_memory\se\ae08.xwb", bank!.VfsPath);
        Assert.Equal("se", bank.Kind);

        string path = new SoundBankCatalog(RetailData.Install).FullPath(bank);
        var waves = SoundBankCatalog.Waves(WaveBank.ReadFile(path), path);
        Assert.NotEmpty(waves);
        Assert.Contains(waves, w => w.Cues.Contains("se_mapg170") || w.Cues.Contains("se_mapg171"));
    }
}
