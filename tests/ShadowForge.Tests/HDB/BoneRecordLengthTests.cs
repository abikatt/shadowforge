using ShadowForge.Formats.HDB;
using ShadowForge.Formats.HDB.Raw;
using ShadowForge.Formats.HDB.Wire;
using ShadowForge.IO;

namespace ShadowForge.Tests.HDB;

/// <summary>
/// Bone records are 80 bytes without ExtraEuler and 104 bytes with it. The runtime picks
/// the size from HFlag bits 0x30, and importer-built files use 80. Reading 104 bytes for
/// a short record would take the next record's header as ExtraEuler.
/// </summary>
public sealed class BoneRecordLengthTests
{
    [Fact]
    public void ShortBoneRecords_ReadZeroExtraEuler_LongRecordsKeepTheirs()
    {
        var file = BuildThreeBoneFile();
        var raw = ModelReader.Read(file);
        var bones = raw.FirstTable.OfType<RawBoneEntry>().ToList();
        Assert.Equal(3, bones.Count);

        Assert.All(bones[0].ExtraEuler, v => Assert.Equal(0f, v));
        Assert.All(bones[1].ExtraEuler, v => Assert.Equal(0f, v));

        Assert.Equal(new[] { 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1.0f }, bones[2].ExtraEuler);
    }

    /// <summary>
    /// ModelWriter must emit each bone at its DiskLength. Writing 0x68 bytes for a 0x50
    /// record overruns the next payload position.
    /// </summary>
    [Fact]
    public void RoundTrip_PreservesShortAndLongBoneLengths()
    {
        var raw = ModelReader.Read(BuildThreeBoneFile());

        var reread = ModelReader.Read(ModelWriter.Write(raw));

        var bones = reread.FirstTable.OfType<RawBoneEntry>().ToList();
        Assert.Equal(3, bones.Count);
        Assert.All(bones[0].ExtraEuler, v => Assert.Equal(0f, v));
        Assert.All(bones[1].ExtraEuler, v => Assert.Equal(0f, v));
        Assert.Equal(new[] { 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1.0f }, bones[2].ExtraEuler);
    }

    private static byte[] BuildThreeBoneFile()
    {
        const int headerSize = FileHeader.Size;
        const int ftCount = 3;
        int entryBase = headerSize + 8;
        int stStart = entryBase + ftCount * FirstTableEntry.Size;
        int payloadBase = stStart + 12;

        int[] lens = { 0x50, 0x50, 0x68 };
        var payloadPos = new int[ftCount];
        int cursor = payloadBase;
        for (int i = 0; i < ftCount; i++)
        {
            payloadPos[i] = cursor;
            cursor += lens[i];
        }
        int fileSize = cursor;
        var data = new byte[fileSize];

        var header = new FileHeader
        {
            Magic = RawHeader.MagicValue,
            FormatVersion = 4,
            FirstTableOffset = 0x0C,
        };
        FileHeader.Write(data.AsSpan(0, headerSize), in header);

        BigEndian.WriteUInt32(data, headerSize, ftCount);
        BigEndian.WriteUInt32(data, headerSize + 4, (uint)(stStart - headerSize - 4));

        for (int i = 0; i < ftCount; i++)
        {
            int entryPos = entryBase + i * FirstTableEntry.Size;
            var entry = new FirstTableEntry
            {
                EntryType = 7,
                DataOffset = (uint)(payloadPos[i] - entryPos - 8),
                DataLength = (uint)lens[i],
            };
            FirstTableEntry.Write(data.AsSpan(entryPos, FirstTableEntry.Size), in entry);
        }

        BigEndian.WriteUInt32(data, stStart, 0);
        BigEndian.WriteUInt32(data, stStart + 4, 4);
        BigEndian.WriteUInt32(data, stStart + 8, 0);

        WriteBone(data, payloadPos[0], lens[0], index: 0, name: "root",
            pos3: (1f, 2f, 3f), extra: null);
        WriteBone(data, payloadPos[1], lens[1], index: 1, name: "child",
            pos3: (-7.62f, 3.05f, 1.5f), extra: null);
        WriteBone(data, payloadPos[2], lens[2], index: 2, name: "orient",
            pos3: (0.25f, 0.5f, 0.75f), extra: new[] { 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1.0f });

        return data;
    }

    private static void WriteBone(
        byte[] data, int pos, int len, uint index, string name,
        (float X, float Y, float Z) pos3, float[]? extra)
    {
        var nameBytes = new byte[16];
        var ascii = System.Text.Encoding.ASCII.GetBytes(name);
        Array.Copy(ascii, nameBytes, Math.Min(ascii.Length, 16));

        var wire = new BoneData
        {
            Index = index,
            HFlag = 0x00100005,
            Position = new System.Numerics.Vector3(pos3.X, pos3.Y, pos3.Z),
            Euler = new System.Numerics.Vector3(0.1f, 0.2f, 0.3f),
            Scale = new System.Numerics.Vector3(1f, 1f, 1f),
            Name = nameBytes,
            ExtraEuler0 = extra?[0] ?? 0f,
            ExtraEuler1 = extra?[1] ?? 0f,
            ExtraEuler2 = extra?[2] ?? 0f,
            ExtraEuler3 = extra?[3] ?? 0f,
            ExtraEuler4 = extra?[4] ?? 0f,
            ExtraEuler5 = extra?[5] ?? 0f,
        };
        Span<byte> full = stackalloc byte[BoneData.LongRecordSize];
        BoneData.Write(full, in wire);
        full[..len].CopyTo(data.AsSpan(pos, len));
    }
}
