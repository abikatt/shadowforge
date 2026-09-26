using ShadowForge.Formats.XACT;

namespace ShadowForge.Tests.Formats.XACT;

public sealed class XmaFileTests
{
    /// <summary>
    /// 5 s of 440/660 Hz stereo at 48 kHz, encoded with xmaencode /S (quality 60): two 64 KiB
    /// blocks, looping the whole file.
    /// </summary>
    internal static readonly string ToneStereo =
        Path.Combine(AppContext.BaseDirectory, "Formats/XACT/Fixtures/tone_stereo.xma");

    [Fact]
    public void Read_EncoderOutput_TakesTheCountsLoopAndSeekTable()
    {
        var xma = XmaFile.ReadFile(ToneStereo);

        Assert.Equal(2, xma.Channels);
        Assert.Equal(48000, xma.SampleRate);
        Assert.Equal(100352, xma.Data.Length);
        Assert.Equal(240640u, xma.SamplesEncoded);
        Assert.Equal(384u, xma.LoopBegin);
        Assert.Equal(240000u, xma.LoopLength);
        Assert.Equal([158208u, 240640u], xma.SeekTable);
    }

    /// <summary>
    /// Retail stereo XMA entries carry 0x82177009, as BG_02_us.xwb's do.
    /// </summary>
    [Fact]
    public void Format_MatchesTheRetailStereoWord() =>
        Assert.Equal(0x82177009u, XmaFile.ReadFile(ToneStereo).Format.Packed);

    [Fact]
    public void Read_PcmWav_ThrowsForTheMissingXma2Chunk()
    {
        byte[] wav = new WavFile { Channels = 1, SampleRate = 48000, Samples = new byte[64] }.Write();
        var ex = Assert.Throws<InvalidDataException>(() => XmaFile.Read(wav, "pcm.wav"));
        Assert.Contains("XMA2", ex.Message);
    }
}
