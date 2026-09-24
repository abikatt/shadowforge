using System.Text;
using ShadowForge.Formats.XACT;
using ShadowForge.IO;

namespace ShadowForge.Tests.Formats.XACT;

public sealed class SoundBankTests
{
    private const string ShippedSystemBank = @"!necessity\bd_system\sound\sys.xsb";
    private const string ShippedEnemyBank = @"snd_memory\se\fdenm.xsb";
    private const string ShippedEnemyWaveBank = @"snd_memory\se\fdenm.xwb";

    [Fact]
    public void Read_Synthetic_ResolvesSimpleAndComplexSounds()
    {
        var bank = SoundBank.Read(BuildBank(), "synthetic");

        Assert.Equal("synth", bank.Name);
        Assert.Equal(40, bank.ToolVersion);
        Assert.Equal(0, bank.ComplexCueCount);
        Assert.Equal(new[] { "synth" }, bank.WaveBankNames);
        Assert.Equal(2, bank.Cues.Count);

        var simple = bank.Cues[0];
        Assert.Equal("se_test001", simple.Name);
        Assert.False(simple.IsComplex);
        Assert.Equal(5, simple.WaveIndex);
        Assert.Equal(0, simple.WaveBankIndex);

        var complex = bank.Cues[1];
        Assert.Equal("se_test002", complex.Name);
        Assert.True(complex.IsComplex);
        Assert.Equal(1, complex.ClipCount);
        Assert.Equal(2, complex.WaveIndex);
        Assert.Equal(0, complex.WaveBankIndex);
    }

    [Fact]
    public void FindCue_IgnoresCase_AndReturnsNullWhenMissing()
    {
        var bank = SoundBank.Read(BuildBank(), "synthetic");
        Assert.Equal(5, bank.FindCue("SE_TEST001")!.WaveIndex);
        Assert.Null(bank.FindCue("se_absent"));
    }

    [Fact]
    public void Read_WrongSignature_Throws()
    {
        var data = BuildBank();
        data[0] = (byte)'S';
        Assert.Throws<InvalidDataException>(() => SoundBank.Read(data, "corrupt"));
    }

    /// <summary>
    /// fdenm's 86 cues are all complex sounds, and their wave indices form an
    /// exact permutation of the 86 entries in fdenm.xwb. Nothing else about the
    /// complex sound layout would produce that.
    /// </summary>
    [SkippableFact]
    public void Read_ShippedEnemyBank_WaveIndicesArePermutation()
    {
        var soundBank = SoundBank.Read(RetailData.Read(ShippedEnemyBank), "fdenm.xsb");
        var waveBank = WaveBank.Read(RetailData.Read(ShippedEnemyWaveBank), "fdenm.xwb");

        Assert.Equal(86, soundBank.Cues.Count);
        Assert.Equal(86, waveBank.Entries.Count);
        Assert.All(soundBank.Cues, c => Assert.True(c.IsComplex));
        Assert.Equal(
            Enumerable.Range(0, 86).ToArray(),
            soundBank.Cues.Select(c => c.WaveIndex).OrderBy(i => i).ToArray());
    }

    /// <summary>
    /// sys has 40 cues over 37 waves, with wave 3 shared by four of them.
    /// </summary>
    [SkippableFact]
    public void Read_ShippedSystemBank_SharesOneWaveAcrossFourCues()
    {
        var bank = SoundBank.Read(RetailData.Read(ShippedSystemBank), "sys.xsb");
        Assert.Equal("sys", bank.Name);
        Assert.Equal(40, bank.Cues.Count);
        Assert.Equal(37, bank.Cues.Select(c => c.WaveIndex).Distinct().Count());
        Assert.All(bank.Cues, c => Assert.False(c.IsComplex));
        Assert.Equal(
            new[] { "se_cfg041", "se_cfg042", "se_cfg043", "se_menu004" },
            bank.Cues.Where(c => c.WaveIndex == 3).Select(c => c.Name).OrderBy(n => n).ToArray());
    }

    /// <summary>
    /// A two-cue bank laid out the way the retail banks are, with the wave bank
    /// name table running from 0x92 up to the sounds table.
    /// </summary>
    private static byte[] BuildBank()
    {
        const int waveBankNames = 0x92;
        const int sounds = 0xD2;
        const int simpleSound = sounds;
        const int complexSound = sounds + 12;
        const int clip = complexSound + 0x0F;
        const int simpleCues = complexSound + 0x27;
        const int cueNameHashTable = simpleCues + 2 * SoundBank.SimpleCueRecordSize;
        const int cueNameValues = cueNameHashTable + 4;
        const int cueNames = cueNameValues + 2 * SoundBank.CueNameValueRecordSize;

        byte[] firstName = Encoding.ASCII.GetBytes("se_test001\0");
        byte[] secondName = Encoding.ASCII.GetBytes("se_test002\0");

        var data = new byte[cueNames + firstName.Length + secondName.Length];
        data[0] = 0x4B; data[1] = 0x42; data[2] = 0x44; data[3] = 0x53;
        BigEndian.WriteUInt16(data, SoundBank.ToolVersionOffset, 40);
        BigEndian.WriteUInt16(data, SoundBank.SimpleCueCountOffset, 2);
        BigEndian.WriteUInt16(data, SoundBank.ComplexCueCountOffset, 0);
        BigEndian.WriteUInt32(data, SoundBank.SimpleCuesOffset, simpleCues);
        BigEndian.WriteUInt32(data, SoundBank.ComplexCuesOffset, SoundBank.NoComplexCues);
        BigEndian.WriteUInt32(data, SoundBank.CueNamesOffset, cueNames);
        BigEndian.WriteUInt32(data, SoundBank.WaveBankNameTableOffset, waveBankNames);
        BigEndian.WriteUInt32(data, SoundBank.CueNameHashTableOffset, cueNameHashTable);
        BigEndian.WriteUInt32(data, SoundBank.CueNameHashValuesOffset, cueNameValues);
        BigEndian.WriteUInt32(data, SoundBank.SoundsOffset, sounds);
        "synth"u8.CopyTo(data.AsSpan(SoundBank.BankNameOffset));
        "synth"u8.CopyTo(data.AsSpan(waveBankNames));

        data[simpleSound + SoundBank.SoundFlagsOffset] = 0x00;
        BigEndian.WriteUInt16(data, simpleSound + SoundBank.SoundCategoryOffset, 1);
        data[simpleSound + SoundBank.SoundVolumeOffset] = 0x5A;
        data[simpleSound + SoundBank.SoundEntryLengthOffset] = 12;
        BigEndian.WriteUInt16(data, simpleSound + SoundBank.SimpleSoundWaveIndexOffset, 5);
        data[simpleSound + SoundBank.SimpleSoundWaveBankIndexOffset] = 0;

        data[complexSound + SoundBank.SoundFlagsOffset] = SoundBank.SoundFlagComplex;
        BigEndian.WriteUInt16(data, complexSound + SoundBank.SoundCategoryOffset, 1);
        data[complexSound + SoundBank.SoundVolumeOffset] = 0x5A;
        data[complexSound + SoundBank.SoundEntryLengthOffset] = 0x27;
        data[complexSound + SoundBank.ComplexSoundClipCountOffset] = 1;
        data[complexSound + SoundBank.ComplexSoundClipTableOffset] = 0xB4;
        BigEndian.WriteUInt32(data, complexSound + SoundBank.ComplexSoundClipTableOffset + 1, clip);
        BigEndian.WriteUInt16(data, clip + SoundBank.ClipWaveIndexOffset, 2);
        data[clip + SoundBank.ClipWaveBankIndexOffset] = 0;

        data[simpleCues] = 0x04;
        BigEndian.WriteUInt32(data, simpleCues + SoundBank.SimpleCueSoundOffsetOffset, simpleSound);
        data[simpleCues + SoundBank.SimpleCueRecordSize] = 0x04;
        BigEndian.WriteUInt32(
            data, simpleCues + SoundBank.SimpleCueRecordSize + SoundBank.SimpleCueSoundOffsetOffset, complexSound);

        BigEndian.WriteUInt32(data, cueNameValues, cueNames);
        BigEndian.WriteUInt32(
            data, cueNameValues + SoundBank.CueNameValueRecordSize, (uint)(cueNames + firstName.Length));

        Array.Copy(firstName, 0, data, cueNames, firstName.Length);
        Array.Copy(secondName, 0, data, cueNames + firstName.Length, secondName.Length);

        return data;
    }
}
