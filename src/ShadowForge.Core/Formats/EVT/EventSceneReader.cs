using System.Text;
using ShadowForge.IO;

namespace ShadowForge.Formats.EVT;

public static class EventSceneReader
{
    /// <summary>
    /// Throws InvalidDataException when the magic is not EVT or a record runs past the end of the file.
    /// </summary>
    public static EventScene Read(byte[] data, string name)
    {
        if (data.Length < EventScene.HeaderSize ||
            data[0] != (byte)'E' || data[1] != (byte)'V' || data[2] != (byte)'T' || data[3] != 0)
            throw new InvalidDataException($"{name}: not an EVT file");

        var scene = new EventScene
        {
            Name = name,
            Version = BigEndian.ReadUInt32(data, EventScene.VersionOffset),
            Checksum = BigEndian.ReadUInt32(data, EventScene.ChecksumOffset),
            Width = BigEndian.ReadInt32(data, EventScene.WidthOffset),
            Height = BigEndian.ReadInt32(data, EventScene.HeightOffset),
            Scale = BigEndian.ReadFloat(data, EventScene.ScaleOffset),
            Frames = BigEndian.ReadInt32(data, EventScene.FramesOffset),
        };
        int trackCount = BigEndian.ReadInt32(data, EventScene.TrackCountOffset);

        int o = EventScene.HeaderSize;
        for (int t = 0; t < trackCount; t++)
        {
            Need(data, o, EventTrack.RecordSize, name, $"track {t} header");
            var track = new EventTrack
            {
                Type = BigEndian.ReadInt32(data, o + EventTrack.TypeOffset),
                Name = ReadName(data, o + EventTrack.NameOffset),
                Label = ReadName(data, o + EventTrack.LabelOffset),
                Flags = BigEndian.ReadUInt32(data, o + EventTrack.FlagsOffset),
                Extra = BigEndian.ReadUInt32(data, o + EventTrack.ExtraOffset),
            };
            int entryCount = BigEndian.ReadInt32(data, o + EventTrack.EntryCountOffset);
            o += EventTrack.RecordSize;

            for (int e = 0; e < entryCount; e++)
            {
                Need(data, o, EventEntry.RecordSize, name, $"track {t} entry {e} header");
                var entry = new EventEntry
                {
                    Frame = BigEndian.ReadInt32(data, o + EventEntry.FrameOffset),
                    Name = ReadName(data, o + EventEntry.NameOffset),
                };
                int keysLength = BigEndian.ReadInt32(data, o + EventEntry.KeysLengthOffset);
                o += EventEntry.RecordSize;
                Need(data, o, keysLength, name, $"track {t} entry {e} keys");
                ReadKeys(data, o, keysLength, entry.Keys);
                o += keysLength;
                track.Entries.Add(entry);
            }
            scene.Tracks.Add(track);
        }
        return scene;
    }

    /// <summary>
    /// Keys are walked by each key's own size, and a type 0 key ends the run. The game
    /// leaves the terminator's size word uninitialized, so it is never read.
    /// </summary>
    private static void ReadKeys(byte[] data, int start, int length, List<EventKey> keys)
    {
        int k = start;
        int end = start + length;
        while (k + EventKey.HeaderSize <= end)
        {
            int type = BigEndian.ReadInt32(data, k + EventKey.TypeOffset);
            if (type == 0)
                break;
            int size = BigEndian.ReadInt32(data, k + EventKey.SizeOffset);
            if (size < EventKey.HeaderSize || k + size > end)
                throw new InvalidDataException(
                    $"key type {type} at 0x{k:X} has size {size} outside its entry");
            var key = new EventKey { Type = type, Size = size, Data = new byte[size - EventKey.HeaderSize] };
            Array.Copy(data, k + EventKey.HeaderSize, key.Data, 0, key.Data.Length);
            keys.Add(key);
            k += size;
        }
    }

    internal static string ReadName(byte[] data, int offset, int length = EventTrack.NameFieldWidth)
    {
        int n = 0;
        while (n < length && offset + n < data.Length && data[offset + n] != 0)
            n++;
        return Encoding.Latin1.GetString(data, offset, n);
    }

    private static void Need(byte[] data, int offset, int size, string name, string what)
    {
        if (size < 0 || offset + size > data.Length)
            throw new InvalidDataException($"{name}: {what} runs past end of file at 0x{offset:X}");
    }
}
