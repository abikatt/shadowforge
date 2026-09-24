using System.Text;
using ShadowForge.Formats.HDB.Raw;
using ShadowForge.Formats.HDB.Wire;
using ShadowForge.IO;

namespace ShadowForge.Formats.HDB;

public static class ModelReader
{
    public static RawModel Read(string path) => Read(File.ReadAllBytes(path));

    public static RawModel Read(byte[] data)
    {
        var raw = new RawModel { Header = ReadHeader(data) };
        ReadFirstTable(data, raw);
        ReadSecondTable(data, raw);

        int tailStart = raw.LastPayloadEnd;
        if (raw.SecondTable.Entries.Length > 0)
        {
            int pos = Align.Up(raw.LastPayloadEnd, 16);
            if (pos + 16 <= data.Length && BigEndian.ReadUInt32(data, pos) == 0)
            {
                raw.PreIaPadding = Slice(data, pos, 16);
                pos += 16;
            }
            pos = ReadIndexBlocks(data, pos, raw);
            tailStart = ReadVertexArrays(data, pos, raw);
        }
        if (tailStart < data.Length)
            raw.TrailingBytes = data[tailStart..];

        ResolveBonePointers(raw);
        return raw;
    }

    private static RawHeader ReadHeader(byte[] data)
    {
        var wire = FileHeader.Read(data.AsSpan(0, FileHeader.Size));
        if (wire.Magic != RawHeader.MagicValue)
            throw new InvalidDataException($"Invalid HDB magic: 0x{wire.Magic:X8}");

        return new RawHeader
        {
            Magic = wire.Magic,
            Flags = wire.Flags,
            FileSizeHint = wire.FileSizeHint,
            FormatVersion = wire.FormatVersion,
            Reserved10 = wire.Reserved10,
            FirstTableOffset = wire.FirstTableOffset,
            Reserved18 = wire.Reserved18,
            Reserved1C = wire.Reserved1C,
        };
    }

    private static void ReadFirstTable(byte[] data, RawModel raw)
    {
        int ftCount = (int)BigEndian.ReadUInt32(data, FileHeader.Size);
        int entryBase = FileHeader.Size + RawLayout.FirstTableHeaderSize;

        for (int i = 0; i < ftCount; i++)
        {
            int entryPos = entryBase + i * FirstTableEntry.Size;
            if (entryPos + FirstTableEntry.Size > data.Length) break;

            var ft = FirstTableEntry.Read(data.AsSpan(entryPos, FirstTableEntry.Size));
            int entryType = (int)ft.EntryType;
            int dataOffset = (int)ft.DataOffset;
            int dataLength = (int)ft.DataLength;

            var e = ReadEntry(data, entryType, entryPos + RawEntry.DataOffsetField + dataOffset, dataLength);
            e.DiskEntryType = entryType;
            e.DiskOffset = dataOffset;
            e.DiskLength = dataLength;
            e.AbsoluteEntryPos = entryPos;
            raw.FirstTable.Add(e);
        }
    }

    /// <summary>
    /// The second table follows the first table's length word plus the length it
    /// declares. Both tables store a length that excludes the count word.
    /// </summary>
    private static void ReadSecondTable(byte[] data, RawModel raw)
    {
        int ftLength = (int)BigEndian.ReadUInt32(data, FileHeader.Size + 4);
        int stStart = FileHeader.Size + ftLength + 4;

        int stCount = (int)BigEndian.ReadUInt32(data, stStart);
        int stLength = (int)BigEndian.ReadUInt32(data, stStart + 4);

        raw.SecondTable.Entries = new int[stCount];
        for (int i = 0; i < stCount; i++)
            raw.SecondTable.Entries[i] = BigEndian.ReadInt32(data, stStart + 8 + i * 4);

        raw.SecondTable.Padding = BigEndian.ReadInt32(data, stStart + stLength + 4);
    }

    /// <summary>
    /// Reads one block per type-3 entry. The index count comes from ByteSize because the
    /// second header word is not an index count.
    /// </summary>
    private static int ReadIndexBlocks(byte[] data, int pos, RawModel raw)
    {
        int blockCount = raw.FirstTable.OfType<RawRenderCommandEntry>().Count();
        for (int b = 0; b < blockCount && pos + IndexBlockHeader.Size <= data.Length; b++)
        {
            var header = IndexBlockHeader.Read(data.AsSpan(pos, IndexBlockHeader.Size));
            int bodyStart = pos + IndexBlockHeader.Size;
            int indexCount = (int)(header.ByteSize / 2);

            var block = new RawIndexBlock
            {
                ByteSize = header.ByteSize,
                ByteSizeDuplicate = header.ByteSizeDuplicate,
                Reserved08 = header.Reserved08,
                Reserved0C = header.Reserved0C,
                Indices = new ushort[indexCount],
            };
            for (int k = 0; k < indexCount; k++)
                block.Indices[k] = BigEndian.ReadUInt16(data, bodyStart + k * 2);

            int padLen = (int)header.ByteSize - indexCount * 2;
            if (padLen > 0)
                block.TrailingPad = Slice(data, bodyStart + indexCount * 2, padLen);

            raw.IndexBlocks.Add(block);
            pos = bodyStart + (int)header.ByteSize;
        }
        return pos;
    }

    private static int ReadVertexArrays(byte[] data, int pos, RawModel raw)
    {
        int recordCount = raw.FirstTable.OfType<RawVASetupEntry>().Sum(vs => vs.Records.Count);
        for (int v = 0; v < recordCount && pos + VAHeaderData.Size <= data.Length; v++)
        {
            var header = VAHeaderData.Read(data.AsSpan(pos, VAHeaderData.Size));
            int bodyStart = pos + VAHeaderData.Size;
            raw.VertexArrays.Add(new RawVertexArray
            {
                ByteSize = header.ByteSize,
                FormatType = header.FormatType,
                VertexCount = header.VertexCount,
                Reserved0C = header.Reserved0C,
                RawBytes = Slice(data, bodyStart, (int)header.ByteSize),
            });
            pos = bodyStart + (int)header.ByteSize;
        }
        return pos;
    }

    /// <summary>
    /// Resolves each bone's ChildPtr and SiblingPtr to the FT position of the bone whose
    /// payload it targets. A zero pointer, or one that hits no bone payload, becomes -1.
    /// </summary>
    private static void ResolveBonePointers(RawModel raw)
    {
        var layout = BoneLayout.Build(raw);
        var ftByPayloadPos = new Dictionary<int, int>(layout.Bones.Count);
        for (int ft = 0; ft < layout.Bones.Count; ft++)
            ftByPayloadPos[layout.FtToPayloadPos[ft]] = ft;

        int Resolve(int payloadPos, int field, int ptr)
            => ptr != 0 && ftByPayloadPos.TryGetValue(payloadPos + field + ptr, out int ft) ? ft : -1;

        for (int ft = 0; ft < layout.Bones.Count; ft++)
        {
            var b = layout.Bones[ft];
            int self = layout.FtToPayloadPos[ft];
            b.NextSiblingFtPos = Resolve(self, BoneData.SiblingPtrOffset, b.ExpectedSiblingPtrDisk);
            b.ChildFtPos = Resolve(self, BoneData.ChildPtrOffset, b.ExpectedChildPtrDisk);
        }
    }

    private static RawEntry ReadEntry(byte[] data, int entryType, int pos, int len) => entryType switch
    {
        0 => new RawPaddingEntry { Payload = SliceClamped(data, pos, len) },
        3 => new RawRenderCommandEntry { Commands = RenderCommandStream.Parse(SliceClamped(data, pos, len)) },
        4 => ReadFaceCount(data, pos, len),
        5 => ReadVASetup(data, pos),
        6 => ReadModel(data, pos),
        7 => ReadBone(data, pos, len),
        9 => ReadUnknown9(data, pos, len),
        10 => ReadTextureTable(data, pos, len),
        11 => ReadTextureCount(data, pos, len),
        12 => new RawUnknownEntry12 { Payload = SliceClamped(data, pos, len) },
        _ => new RawUnknownGenericEntry { Payload = SliceClamped(data, pos, len) },
    };

    private static RawEntry ReadFaceCount(byte[] data, int pos, int len)
    {
        var entry = new RawFaceCountEntry { Count = BigEndian.ReadUInt32(data, pos) };
        if (len >= 8)
            entry.IndexBufferHandleSlot = BigEndian.ReadUInt32(data, pos + 4);
        return entry;
    }

    private static RawEntry ReadVASetup(byte[] data, int pos)
    {
        var entry = new RawVASetupEntry();
        uint count = BigEndian.ReadUInt32(data, pos);
        for (int j = 0; j < count; j++)
        {
            var rec = VASetupRecord.Read(data.AsSpan(pos + 4 + j * VASetupRecord.Size, VASetupRecord.Size));
            entry.Records.Add(new RawVASetupRecord
            {
                VertexCount = rec.VertexCount,
                FormatType = rec.FormatType,
                Offset = rec.Offset,
            });
        }
        return entry;
    }

    private static RawEntry ReadModel(byte[] data, int pos)
    {
        var w = ModelRecord.Read(data.AsSpan(pos, ModelRecord.Size));
        return new RawModelEntry
        {
            RenderCommandPtr = w.RenderCommandPtr,
            IndexBlockCount = w.IndexBlockCount,
            IndexTablePtr = w.IndexTablePtr,
            VertexArrayCount = w.VertexArrayCount,
            VASetupPtr = w.VASetupPtr,
            SphereCenter = w.SphereCenter,
            SphereRadiusBits = w.SphereRadiusBits,
        };
    }

    /// <summary>
    /// A record shorter than the long form reads as zeros past its end.
    /// </summary>
    private static RawEntry ReadBone(byte[] data, int pos, int len)
    {
        Span<byte> record = stackalloc byte[BoneData.LongRecordSize];
        int avail = Math.Min(len, Math.Min(BoneData.LongRecordSize, data.Length - pos));
        data.AsSpan(pos, avail).CopyTo(record);
        var wire = BoneData.Read(record);
        var bone = new RawBoneEntry
        {
            Index = wire.Index,
            Reserved04 = wire.Reserved04,
            HFlag = wire.HFlag,
            PackedFlags0C = wire.PackedFlags0C,
            Position = wire.Position,
            Euler = wire.Euler,
            Reserved28 = wire.Reserved28,
            Scale = wire.Scale,
            ExpectedChildPtrDisk = wire.ChildPtr,
            ExpectedSiblingPtrDisk = wire.SiblingPtr,
            Name = DecodeName(wire.Name),
            ExtraEuler = new[]
            {
                wire.ExtraEuler0, wire.ExtraEuler1, wire.ExtraEuler2,
                wire.ExtraEuler3, wire.ExtraEuler4, wire.ExtraEuler5,
            },
        };

        if (len > BoneData.LongRecordSize)
            bone.TrailingBytes = Slice(data, pos + BoneData.LongRecordSize, len - BoneData.LongRecordSize);

        if (wire.ChildPtr != 0)
        {
            int childTarget = pos + BoneData.ChildPtrOffset + wire.ChildPtr;
            if (childTarget >= 0 && childTarget + 4 <= data.Length)
                bone.ChildIndex = (int)BigEndian.ReadUInt32(data, childTarget);
        }
        return bone;
    }

    private static RawEntry ReadUnknown9(byte[] data, int pos, int len)
    {
        var entry = new RawUnknownEntry9();
        int count = len / RawUnknownEntry9.RecordSize;
        for (int j = 0; j < count; j++)
        {
            var rec = new uint[6];
            for (int k = 0; k < 6; k++)
                rec[k] = BigEndian.ReadUInt32(data, pos + j * RawUnknownEntry9.RecordSize + k * 4);
            entry.Records.Add(rec);
        }
        return entry;
    }

    private static RawEntry ReadTextureTable(byte[] data, int pos, int len)
    {
        var entry = new RawTextureTableEntry();
        int count = len / TextureData.Size;
        for (int j = 0; j < count; j++)
        {
            var wire = TextureData.Read(data.AsSpan(pos + j * TextureData.Size, TextureData.Size));
            entry.Records.Add(new RawTextureRecord
            {
                Name = DecodeName(wire.Name),
                FloatParam = wire.FloatParam,
                FlagField18 = wire.FlagField18,
            });
        }
        return entry;
    }

    private static RawEntry ReadTextureCount(byte[] data, int pos, int len)
    {
        var entry = new RawTextureCountEntry { Count = BigEndian.ReadUInt32(data, pos) };
        if (len >= 8)
            entry.OffsetToTextureTable = BigEndian.ReadUInt32(data, pos + 4);
        return entry;
    }

    private static string DecodeName(byte[] name) => Encoding.ASCII.GetString(name).TrimEnd('\0');

    private static byte[] Slice(byte[] data, int pos, int len) => data.AsSpan(pos, len).ToArray();

    /// <summary>
    /// A <paramref name="len"/>-byte buffer holding the part of the range that lies inside
    /// <paramref name="data"/>. The rest stays zero.
    /// </summary>
    private static byte[] SliceClamped(byte[] data, int pos, int len)
    {
        var payload = new byte[len];
        Array.Copy(data, pos, payload, 0, Math.Min(len, data.Length - pos));
        return payload;
    }
}
