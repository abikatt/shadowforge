using ShadowForge.Formats.HMB;
using ShadowForge.IO;

namespace ShadowForge.Tests.HMB;

public sealed class MotionReaderTests
{
    private const string Gm002 = "testdata/models/gimmick/gm002/gm002_01/gm002_01.hmb";

    private static MotionClip ReadGm002()
        => MotionReader.Read(File.ReadAllBytes(Gm002), "gm002_01");

    [Fact]
    public void Gm002_Header()
    {
        var clip = ReadGm002();
        Assert.Equal(2, clip.Mode);
        Assert.Equal(7, clip.DurationFrames);
        Assert.Equal(30f, clip.Rate);
        Assert.Equal(0f, clip.RateFlag);
        Assert.Equal(30f, clip.EffectiveRate);
        Assert.Equal(1, clip.FrameDivisor);
        Assert.Equal(2, clip.Tracks.Count);
        Assert.Empty(clip.ExtraSectionTypes);
    }

    [Fact]
    public void Gm002_TrackNamesAndCounts()
    {
        var clip = ReadGm002();
        var t0 = clip.Tracks[0];
        Assert.Equal("huta02", t0.BoneName);
        Assert.Equal(3, t0.Translation!.Linear!.Length);
        Assert.Equal(4, t0.Rotation!.Linear!.Length);
        Assert.Equal(3, t0.Scale!.Linear!.Length);

        var t1 = clip.Tracks[1];
        Assert.Equal("huta01", t1.BoneName);
        Assert.Single(t1.Translation!.Linear!);
        Assert.Equal(8, t1.Rotation!.Linear!.Length);
        Assert.Single(t1.Scale!.Linear!);
    }

    [Fact]
    public void Gm002_RotationKeys_Track1()
    {
        var keys = ReadGm002().Tracks[1].Rotation!.Linear!;
        short[] frames = keys.Select(k => k.Frame).ToArray();
        Assert.Equal(new short[] { 0, 1, 2, 3, 4, 5, 6, 7 }, frames);
        Assert.Equal(0, keys[0].X);
        Assert.Equal(unchecked((short)0xE837), keys[1].X);
        Assert.Equal(unchecked((short)0xD7BB), keys[2].X);
        Assert.Equal(unchecked((short)0xC5B1), keys[7].X);
        Assert.All(keys, k => Assert.Equal(0, k.Y));
        Assert.All(keys, k => Assert.Equal(0, k.Z));
    }

    [Fact]
    public void Gm002_ConstantTranslation_Track1()
    {
        var keys = ReadGm002().Tracks[1].Translation!.Linear!;
        var v = keys[0].Value;
        Assert.Equal(0, keys[0].Frame);

        Assert.Equal(BitConverter.UInt32BitsToSingle(0x34617C5Eu), v.X);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0x40ED56FBu), v.Y);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0xC080C624u), v.Z);
    }

    [Fact]
    public void Read_BadMagic_Throws()
    {
        var data = File.ReadAllBytes(Gm002);
        data[0] = (byte)'X';
        Assert.Throws<InvalidDataException>(() => MotionReader.Read(data, "x"));
    }

    [Fact]
    public void Read_RateFlagPositiveNonMultipleOf30_Throws()
    {
        var data = File.ReadAllBytes(Gm002);

        int p = (int)BigEndian.ReadUInt32(data, 0x14) + 0x14;
        int ftCount = (int)BigEndian.ReadUInt32(data, p);
        int motHdr = -1;
        for (int i = 0; i < ftCount; i++)
        {
            int entry = p + 8 + i * 16;
            int type = (int)BigEndian.ReadUInt32(data, entry);
            if (type == 0x105)
            {
                int relField = entry + 8;
                motHdr = relField + BigEndian.ReadInt32(data, relField);
                break;
            }
        }
        Assert.True(motHdr >= 0, "expected an FT entry of type 0x105 (motion section)");

        BigEndian.WriteFloat(data, motHdr + 12, 45.0f);
        BigEndian.WriteFloat(data, motHdr + 16, 1.0f);

        Assert.Throws<InvalidDataException>(() => MotionReader.Read(data, "gm002_01"));
    }
}
