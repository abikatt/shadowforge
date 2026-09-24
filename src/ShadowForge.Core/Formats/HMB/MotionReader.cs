using System.Numerics;
using ShadowForge.IO;

namespace ShadowForge.Formats.HMB;

public static class MotionReader
{
    /// <summary>
    /// Only modes 2 and 3 are accepted, the two that ship in mot.mpk. A positive rate flag
    /// requires the rate to be a positive multiple of 30.
    /// </summary>
    public static MotionClip Read(byte[] data, string clipName)
    {
        uint magic = BigEndian.ReadUInt32(data, 0);
        if (magic != MotionFile.Magic)
            throw new InvalidDataException($"{clipName}: not an HMB file (magic 0x{magic:X8})");

        int p = MotionFile.FirstTableStart(BigEndian.ReadUInt32(data, MotionFile.FirstTableOffsetField));
        int ftCount = (int)BigEndian.ReadUInt32(data, p);
        int motHdr = -1;
        var clip = new MotionClip { Name = clipName };

        for (int i = 0; i < ftCount; i++)
        {
            int entry = p + MotionFile.FirstTableEntriesOffset + i * MotionFile.FirstTableEntrySize;
            int type = (int)BigEndian.ReadUInt32(data, entry);
            if (type == (int)MotionSection.Motion)
                motHdr = Follow(data, entry + MotionFile.FirstTableEntryDataField);
            else if (type != (int)MotionSection.Relocated)
                clip.ExtraSectionTypes.Add(type);
        }
        if (motHdr < 0)
            throw new InvalidDataException($"{clipName}: no motion section (FT type 0x105)");

        int trackArray = Follow(data, motHdr);
        clip.DurationFrames = BigEndian.ReadUInt16(data, motHdr + MotionFile.MotionDurationField);
        clip.Mode = BigEndian.ReadUInt16(data, motHdr + MotionFile.MotionModeField);
        int trackCount = BigEndian.ReadUInt16(data, motHdr + MotionFile.MotionTrackCountField);
        clip.Rate = BigEndian.ReadFloat(data, motHdr + MotionFile.MotionRateField);
        clip.RateFlag = BigEndian.ReadFloat(data, motHdr + MotionFile.MotionRateFlagField);

        if (clip.RateFlag > 0f &&
            (clip.Rate <= 0f || clip.Rate % MotionClip.DefaultFrameRate != 0f))
        {
            throw new InvalidDataException(
                $"{clipName}: rateFlag={clip.RateFlag} requires rate to be a positive exact multiple of " +
                $"{MotionClip.DefaultFrameRate}, got rate={clip.Rate}");
        }

        if (clip.Mode != 2 && clip.Mode != 3)
            throw new InvalidDataException(
                $"{clipName}: unsupported mode {clip.Mode} (only 2/3 ship via mot.mpk)");

        for (int i = 0; i < trackCount; i++)
        {
            int rec = trackArray + i * MotionTrack.RecordSize;
            int cT = BigEndian.ReadUInt16(data, rec + MotionTrack.TranslationCountField);
            int cR = BigEndian.ReadUInt16(data, rec + MotionTrack.RotationCountField);
            int cS = BigEndian.ReadUInt16(data, rec + MotionTrack.ScaleCountField);
            clip.Tracks.Add(new MotionTrack
            {
                BoneName = data.DecodeASCII(rec + MotionTrack.NameField, MotionTrack.NameFieldSize),
                Translation = ReadVec3Channel(data, rec + MotionTrack.TranslationDataField, cT, clip.Mode),
                Rotation = ReadEulerChannel(data, rec + MotionTrack.RotationDataField, cR, clip.Mode),
                Scale = ReadVec3Channel(data, rec + MotionTrack.ScaleDataField, cS, clip.Mode),
            });
        }
        return clip;
    }

    private static int Follow(byte[] data, int relField) => relField + BigEndian.ReadInt32(data, relField);

    private static bool IsLinear(int count, int mode) => count == 1 || mode == 2;

    private static Vec3Channel? ReadVec3Channel(byte[] data, int relField, int count, int mode)
    {
        if (BigEndian.ReadInt32(data, relField) == 0) return null;
        int ptr = Follow(data, relField);

        if (!IsLinear(count, mode))
            return new Vec3Channel { Axes = ReadAxes(data, ptr, isAngle: false) };

        var keys = new LinearVec3Key[count];
        for (int k = 0; k < count; k++)
        {
            int kp = ptr + k * LinearVec3Key.Size;
            keys[k] = new LinearVec3Key(
                BigEndian.ReadInt16(data, kp),
                new Vector3(
                    BigEndian.ReadFloat(data, kp + 4),
                    BigEndian.ReadFloat(data, kp + 8),
                    BigEndian.ReadFloat(data, kp + 12)));
        }
        return new Vec3Channel { Linear = keys };
    }

    private static EulerChannel? ReadEulerChannel(byte[] data, int relField, int count, int mode)
    {
        if (BigEndian.ReadInt32(data, relField) == 0) return null;
        int ptr = Follow(data, relField);

        if (!IsLinear(count, mode))
            return new EulerChannel { Axes = ReadAxes(data, ptr, isAngle: true) };

        var keys = new LinearEulerKey[count];
        for (int k = 0; k < count; k++)
        {
            int kp = ptr + k * LinearEulerKey.Size;
            keys[k] = new LinearEulerKey(
                BigEndian.ReadInt16(data, kp),
                BigEndian.ReadInt16(data, kp + 2),
                BigEndian.ReadInt16(data, kp + 4),
                BigEndian.ReadInt16(data, kp + 6));
        }
        return new EulerChannel { Linear = keys };
    }

    /// <summary>
    /// Three 8-byte axis headers, each a u32 key count and a self-relative s32 to its keys.
    /// </summary>
    private static HermiteCurve[] ReadAxes(byte[] data, int block, bool isAngle)
    {
        var axes = new HermiteCurve[3];
        for (int a = 0; a < 3; a++)
        {
            int cnt = (int)BigEndian.ReadUInt32(data, block + a * 8);
            int keysPtr = Follow(data, block + a * 8 + 4);
            var keys = new HermiteKey[cnt];
            for (int k = 0; k < cnt; k++)
            {
                int kp = keysPtr + k * HermiteKey.Size;
                keys[k] = new HermiteKey(
                    BigEndian.ReadInt16(data, kp),
                    BigEndian.ReadUInt16(data, kp + 2),
                    BigEndian.ReadFloat(data, kp + 4));
            }
            axes[a] = new HermiteCurve { IsAngle = isAngle, Keys = keys };
        }
        return axes;
    }
}
