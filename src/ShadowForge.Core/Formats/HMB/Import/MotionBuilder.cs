using System.Text;
using ShadowForge.IO;

namespace ShadowForge.Formats.HMB.Import;

/// <summary>
/// Writes a single-section mode-2 HMB clip, in file order:
/// the 0x20-byte header,
/// the first table {count 1, offset to second table} with one entry {0x105, 0, offset to
/// motion header, motion header size},
/// the second table {slot count 1, offset to trailer, slot 0 = offset to the first-table
/// entry's data field},
/// the trailer {u32 12, 8 zero bytes},
/// key data per track in T, R, S order for the channels present,
/// the track records, 4 zero bytes, and the motion header at EOF.
/// Every stored offset is self-relative: target minus the field's own position.
/// </summary>
public static class MotionBuilder
{
    private const int MotionHeaderPad = 4;

    public static byte[] Build(MotionClip clip)
    {
        Validate(clip);

        const int p = MotionFile.HeaderSize;
        const int entry = p + MotionFile.FirstTableEntriesOffset;
        const int entryDataField = entry + MotionFile.FirstTableEntryDataField;
        const int q = entry + MotionFile.FirstTableEntrySize;
        const int slotCount = 1;
        const int slot0 = q + 8;
        const int trailer = slot0 + slotCount * 4;
        const int trailerSize = 12;
        const int dataBase = trailer + trailerSize;

        var keyOffsets = new (int T, int R, int S)[clip.Tracks.Count];
        int cursor = dataBase;
        for (int i = 0; i < clip.Tracks.Count; i++)
        {
            var track = clip.Tracks[i];
            int tOff = 0, rOff = 0, sOff = 0;
            if (track.Translation?.Linear is { } tk) { tOff = cursor; cursor += tk.Length * LinearVec3Key.Size; }
            if (track.Rotation?.Linear is { } rk) { rOff = cursor; cursor += rk.Length * LinearEulerKey.Size; }
            if (track.Scale?.Linear is { } sk) { sOff = cursor; cursor += sk.Length * LinearVec3Key.Size; }
            keyOffsets[i] = (tOff, rOff, sOff);
        }

        int trackArray = cursor;
        int motHdr = trackArray + clip.Tracks.Count * MotionTrack.RecordSize + MotionHeaderPad;
        int fileSize = motHdr + MotionFile.MotionHeaderSize;

        var d = new byte[fileSize];

        BigEndian.WriteUInt32(d, 0x00, MotionFile.Magic);
        BigEndian.WriteUInt32(d, 0x04, MotionFile.Version);
        BigEndian.WriteUInt32(d, 0x08, (uint)fileSize);
        BigEndian.WriteUInt32(d, 0x0C, 4);
        WriteRelative(d, MotionFile.FirstTableOffsetField, p);

        BigEndian.WriteUInt32(d, p, 1);
        WriteRelative(d, p + 4, q);
        BigEndian.WriteUInt32(d, entry, (uint)MotionSection.Motion);
        WriteRelative(d, entryDataField, motHdr);
        BigEndian.WriteUInt32(d, entry + 12, MotionFile.MotionHeaderSize);

        BigEndian.WriteUInt32(d, q, slotCount);
        WriteRelative(d, q + 4, trailer);
        WriteRelative(d, slot0, entryDataField);

        BigEndian.WriteUInt32(d, trailer, trailerSize);

        for (int i = 0; i < clip.Tracks.Count; i++)
        {
            var track = clip.Tracks[i];
            var (tOff, rOff, sOff) = keyOffsets[i];
            if (track.Translation?.Linear is { } tk) WriteVec3Keys(d, tOff, tk);
            if (track.Rotation?.Linear is { } rk) WriteEulerKeys(d, rOff, rk);
            if (track.Scale?.Linear is { } sk) WriteVec3Keys(d, sOff, sk);

            int rec = trackArray + i * MotionTrack.RecordSize;
            WriteOptionalRelative(d, rec + MotionTrack.TranslationDataField, tOff);
            WriteOptionalRelative(d, rec + MotionTrack.RotationDataField, rOff);
            WriteOptionalRelative(d, rec + MotionTrack.ScaleDataField, sOff);

            BigEndian.WriteUInt16(d, rec + MotionTrack.TranslationCountField, (ushort)(track.Translation?.Linear?.Length ?? 0));
            BigEndian.WriteUInt16(d, rec + MotionTrack.RotationCountField, (ushort)(track.Rotation?.Linear?.Length ?? 0));
            BigEndian.WriteUInt16(d, rec + MotionTrack.ScaleCountField, (ushort)(track.Scale?.Linear?.Length ?? 0));

            Encoding.ASCII.GetBytes(track.BoneName).CopyTo(d, rec + MotionTrack.NameField);
        }

        WriteRelative(d, motHdr, trackArray);
        BigEndian.WriteUInt16(d, motHdr + MotionFile.MotionDurationField, (ushort)clip.DurationFrames);
        BigEndian.WriteUInt16(d, motHdr + MotionFile.MotionModeField, 2);
        BigEndian.WriteUInt16(d, motHdr + MotionFile.MotionTrackCountField, (ushort)clip.Tracks.Count);
        BigEndian.WriteFloat(d, motHdr + MotionFile.MotionRateField, clip.Rate);
        BigEndian.WriteFloat(d, motHdr + MotionFile.MotionRateFlagField, clip.RateFlag);

        return d;
    }

    /// <summary>
    /// Rejects hermite channels, which must be baked to linear keys first, and any count or
    /// name that does not fit its field.
    /// </summary>
    private static void Validate(MotionClip clip)
    {
        foreach (var track in clip.Tracks)
        {
            if (track.Translation?.Axes != null || track.Rotation?.Axes != null || track.Scale?.Axes != null)
                throw new InvalidOperationException(
                    $"track '{track.BoneName}' carries hermite (Axes) channels. Bake to linear before building mode-2 HMB.");
        }

        if (clip.Tracks.Count > ushort.MaxValue)
            throw new InvalidDataException($"clip has {clip.Tracks.Count} tracks but the track count field is u16.");

        foreach (var track in clip.Tracks)
        {
            int nameLength = Encoding.ASCII.GetByteCount(track.BoneName);
            if (nameLength > MotionTrack.NameFieldSize - 1)
                throw new InvalidDataException(
                    $"track name '{track.BoneName}' is {nameLength} bytes, max is {MotionTrack.NameFieldSize - 1} ASCII characters.");

            int cT = track.Translation?.Linear?.Length ?? 0;
            int cR = track.Rotation?.Linear?.Length ?? 0;
            int cS = track.Scale?.Linear?.Length ?? 0;
            if (cT > ushort.MaxValue || cR > ushort.MaxValue || cS > ushort.MaxValue)
                throw new InvalidDataException(
                    $"track '{track.BoneName}' has a key count exceeding u16.");
        }
    }

    private static void WriteRelative(byte[] d, int field, int target)
        => BigEndian.WriteInt32(d, field, target - field);

    /// <summary>
    /// A zero target means the channel is absent and is stored as 0.
    /// </summary>
    private static void WriteOptionalRelative(byte[] d, int field, int target)
        => BigEndian.WriteInt32(d, field, target == 0 ? 0 : target - field);

    private static void WriteVec3Keys(byte[] d, int offset, LinearVec3Key[] keys)
    {
        for (int k = 0; k < keys.Length; k++)
        {
            int kp = offset + k * LinearVec3Key.Size;
            BigEndian.WriteInt32(d, kp, keys[k].Frame << 16);
            BigEndian.WriteFloat(d, kp + 4, keys[k].Value.X);
            BigEndian.WriteFloat(d, kp + 8, keys[k].Value.Y);
            BigEndian.WriteFloat(d, kp + 12, keys[k].Value.Z);
        }
    }

    private static void WriteEulerKeys(byte[] d, int offset, LinearEulerKey[] keys)
    {
        for (int k = 0; k < keys.Length; k++)
        {
            int kp = offset + k * LinearEulerKey.Size;
            BigEndian.WriteInt16(d, kp, keys[k].Frame);
            BigEndian.WriteInt16(d, kp + 2, keys[k].X);
            BigEndian.WriteInt16(d, kp + 4, keys[k].Y);
            BigEndian.WriteInt16(d, kp + 6, keys[k].Z);
        }
    }
}
