using System.Text;
using ShadowForge.Formats.HDB.Raw;
using ShadowForge.Formats.HDB.Wire;
using ShadowForge.IO;

namespace ShadowForge.Formats.HDB;

public static class ModelWriter
{
    public static byte[] Write(RawModel raw)
    {
        using var ms = new MemoryStream();
        using var w = new BigEndianWriter(ms);

        WriteHeader(w, raw.Header);
        WriteFirstTable(w, raw);
        WriteSecondTable(w, raw.SecondTable);
        WritePayloads(w, raw);

        w.WriteZeros(Align.Up((int)w.Position, 16) - (int)w.Position);
        w.WriteBytes(raw.PreIaPadding);
        foreach (var block in raw.IndexBlocks)
            WriteIndexBlock(w, block);
        foreach (var va in raw.VertexArrays)
            WriteVertexArray(w, va);
        w.WriteBytes(raw.TrailingBytes);

        return ms.ToArray();
    }

    private static void WriteHeader(BigEndianWriter w, RawHeader header)
    {
        Span<byte> buf = stackalloc byte[FileHeader.Size];
        FileHeader.Write(buf, new FileHeader
        {
            Magic = header.Magic,
            Flags = header.Flags,
            FileSizeHint = header.FileSizeHint,
            FormatVersion = header.FormatVersion,
            Reserved10 = header.Reserved10,
            FirstTableOffset = header.FirstTableOffset,
            Reserved18 = header.Reserved18,
            Reserved1C = header.Reserved1C,
        });
        w.BaseStream.Write(buf);
    }

    private static void WriteFirstTable(BigEndianWriter w, RawModel raw)
    {
        w.WriteUInt32((uint)raw.FirstTable.Count);
        w.WriteUInt32((uint)(4 + raw.FirstTable.Count * FirstTableEntry.Size));

        Span<byte> buf = stackalloc byte[FirstTableEntry.Size];
        foreach (var entry in raw.FirstTable)
        {
            FirstTableEntry.Write(buf, new FirstTableEntry
            {
                EntryType = (uint)entry.DiskEntryType,
                DataOffset = (uint)entry.DiskOffset,
                DataLength = (uint)entry.DiskLength,
            });
            w.BaseStream.Write(buf);
        }
    }

    private static void WriteSecondTable(BigEndianWriter w, RawSecondTable table)
    {
        w.WriteUInt32((uint)table.Entries.Length);
        w.WriteUInt32((uint)(4 + table.Entries.Length * 4));
        foreach (var v in table.Entries)
            w.WriteInt32(v);
        w.WriteInt32(table.Padding);
    }

    /// <summary>
    /// Emits payloads sorted by position, since some files order payloads differently
    /// from their FT records. Throws when two payloads overlap.
    /// </summary>
    private static void WritePayloads(BigEndianWriter w, RawModel raw)
    {
        var layout = BoneLayout.Build(raw);
        foreach (var entry in raw.FirstTable.OrderBy(e => e.PayloadPos))
        {
            long cur = w.Position;
            if (cur > entry.PayloadPos)
                throw new InvalidOperationException(
                    $"Writer overshot: at 0x{cur:X}, target 0x{entry.PayloadPos:X}. " +
                    "Two FT entry payloads overlap.");
            w.WriteZeros(entry.PayloadPos - (int)cur);
            WritePayload(w, entry, layout);
        }
    }

    private static void WritePayload(BigEndianWriter w, RawEntry entry, BoneLayout layout)
    {
        switch (entry)
        {
            case RawPaddingEntry p:
                w.WriteBytes(p.Payload);
                break;
            case RawRenderCommandEntry rc:
                w.WriteBytes(RenderCommandStream.Write(rc.Commands));
                break;
            case RawFaceCountEntry fc:
                w.WriteUInt32(fc.Count);
                if (fc.DiskLength >= 8)
                    w.WriteUInt32(fc.IndexBufferHandleSlot);
                break;
            case RawVASetupEntry vs:
                WriteVASetup(w, vs);
                break;
            case RawModelEntry model:
                WriteModel(w, model);
                break;
            case RawBoneEntry bone:
                WriteBone(w, bone, layout);
                break;
            case RawUnknownEntry9 u9:
                foreach (var rec in u9.Records)
                    foreach (var v in rec)
                        w.WriteUInt32(v);
                break;
            case RawTextureTableEntry tt:
                WriteTextureTable(w, tt);
                break;
            case RawTextureCountEntry tc:
                w.WriteUInt32(tc.Count);
                if (tc.DiskLength >= 8)
                    w.WriteUInt32(tc.OffsetToTextureTable);
                break;
            case RawUnknownEntry12 u12:
                w.WriteBytes(u12.Payload);
                break;
            case RawUnknownGenericEntry gen:
                w.WriteBytes(gen.Payload);
                break;
            default:
                throw new NotSupportedException($"Unknown RawEntry subclass {entry.GetType().Name}");
        }
    }

    private static void WriteVASetup(BigEndianWriter w, RawVASetupEntry vs)
    {
        w.WriteUInt32((uint)vs.Records.Count);
        Span<byte> buf = stackalloc byte[VASetupRecord.Size];
        foreach (var rec in vs.Records)
        {
            VASetupRecord.Write(buf, new VASetupRecord
            {
                VertexCount = rec.VertexCount,
                FormatType = rec.FormatType,
                Offset = rec.Offset,
            });
            w.BaseStream.Write(buf);
        }
    }

    private static void WriteModel(BigEndianWriter w, RawModelEntry model)
    {
        Span<byte> buf = stackalloc byte[ModelRecord.Size];
        ModelRecord.Write(buf, new ModelRecord
        {
            RenderCommandPtr = model.RenderCommandPtr,
            IndexBlockCount = model.IndexBlockCount,
            IndexTablePtr = model.IndexTablePtr,
            VertexArrayCount = model.VertexArrayCount,
            VASetupPtr = model.VASetupPtr,
            SphereCenter = model.SphereCenter,
            SphereRadiusBits = model.SphereRadiusBits,
        });
        w.BaseStream.Write(buf);
    }

    /// <summary>
    /// Recomputes ChildPtr and SiblingPtr from the tree links and throws when either
    /// differs from the value read or set by RawLayout. Writes the short or long record
    /// form as DiskLength asks.
    /// </summary>
    private static void WriteBone(BigEndianWriter w, RawBoneEntry bone, BoneLayout layout)
    {
        int ft = layout.GetFt(bone);

        int childPtr = layout.ComputeChildPtr(ft);
        if (childPtr != bone.ExpectedChildPtrDisk)
            throw new InvalidDataException(
                $"Child ptr mismatch on bone FT={ft} Index={bone.Index}: " +
                $"computed={childPtr}, expected={bone.ExpectedChildPtrDisk}");

        int siblingPtr = layout.ComputeSiblingPtr(ft);
        if (siblingPtr != bone.ExpectedSiblingPtrDisk)
            throw new InvalidDataException(
                $"Sibling ptr mismatch on bone FT={ft} Index={bone.Index}: " +
                $"computed={siblingPtr}, expected={bone.ExpectedSiblingPtrDisk}");

        Span<byte> buf = stackalloc byte[BoneData.LongRecordSize];
        BoneData.Write(buf, new BoneData
        {
            Index = bone.Index,
            Reserved04 = bone.Reserved04,
            HFlag = bone.HFlag,
            PackedFlags0C = bone.PackedFlags0C,
            Position = bone.Position,
            Euler = bone.Euler,
            Reserved28 = bone.Reserved28,
            Scale = bone.Scale,
            ChildPtr = childPtr,
            SiblingPtr = siblingPtr,
            Name = EncodeName(bone.Name, BoneData.NameWidth),
            ExtraEuler0 = bone.ExtraEuler[0],
            ExtraEuler1 = bone.ExtraEuler[1],
            ExtraEuler2 = bone.ExtraEuler[2],
            ExtraEuler3 = bone.ExtraEuler[3],
            ExtraEuler4 = bone.ExtraEuler[4],
            ExtraEuler5 = bone.ExtraEuler[5],
        });

        if (bone.DiskLength < BoneData.ShortRecordSize)
            throw new InvalidDataException(
                $"Bone FT={ft} has DiskLength {bone.DiskLength}, below the " +
                $"{BoneData.ShortRecordSize}-byte short record.");
        w.BaseStream.Write(buf[..Math.Min(bone.DiskLength, buf.Length)]);
        w.WriteBytes(bone.TrailingBytes);
    }

    private static void WriteTextureTable(BigEndianWriter w, RawTextureTableEntry tt)
    {
        Span<byte> buf = stackalloc byte[TextureData.Size];
        foreach (var rec in tt.Records)
        {
            TextureData.Write(buf, new TextureData
            {
                Name = EncodeName(rec.Name, TextureData.NameWidth),
                FloatParam = rec.FloatParam,
                FlagField18 = rec.FlagField18,
            });
            w.BaseStream.Write(buf);
        }
    }

    private static void WriteIndexBlock(BigEndianWriter w, RawIndexBlock block)
    {
        Span<byte> buf = stackalloc byte[IndexBlockHeader.Size];
        IndexBlockHeader.Write(buf, new IndexBlockHeader
        {
            ByteSize = block.ByteSize,
            ByteSizeDuplicate = block.ByteSizeDuplicate,
            Reserved08 = block.Reserved08,
            Reserved0C = block.Reserved0C,
        });
        w.BaseStream.Write(buf);
        foreach (var idx in block.Indices)
            w.WriteUInt16(idx);
        w.WriteBytes(block.TrailingPad);
    }

    private static void WriteVertexArray(BigEndianWriter w, RawVertexArray va)
    {
        Span<byte> buf = stackalloc byte[VAHeaderData.Size];
        VAHeaderData.Write(buf, new VAHeaderData
        {
            ByteSize = va.ByteSize,
            FormatType = va.FormatType,
            VertexCount = va.VertexCount,
            Reserved0C = va.Reserved0C,
        });
        w.BaseStream.Write(buf);
        w.WriteBytes(va.RawBytes);
    }

    /// <summary>
    /// NUL-padded ASCII of exactly <paramref name="width"/> bytes. Longer names are truncated.
    /// </summary>
    private static byte[] EncodeName(string name, int width)
    {
        var bytes = new byte[width];
        var ascii = Encoding.ASCII.GetBytes(name);
        Array.Copy(ascii, bytes, Math.Min(ascii.Length, width));
        return bytes;
    }
}
