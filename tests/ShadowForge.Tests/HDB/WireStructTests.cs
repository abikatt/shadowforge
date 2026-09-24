using ShadowForge.IO;
using ShadowForge.Formats.HDB.Wire;

namespace ShadowForge.Tests.HDB;

public sealed class WireStructTests
{
    [Fact]
    public void FirstTableEntry_RoundTrip_ByteIdentical()
    {
        var data = File.ReadAllBytes(TestFile.HDB);

        int entryPos = 32 + 8;
        var span = data.AsSpan(entryPos, 0x10);

        var entry = FirstTableEntry.Read(span);
        var output = new byte[0x10];
        FirstTableEntry.Write(output, in entry);

        Assert.Equal(span.ToArray(), output);
    }

    [Fact]
    public void BoneData_RoundTrip_ByteIdentical()
    {
        var data = File.ReadAllBytes(TestFile.HDB);

        int ftStart = 32;
        int ftCount = (int)BigEndian.ReadUInt32(data, ftStart);
        int entryBase = ftStart + 8;

        for (int i = 0; i < ftCount; i++)
        {
            int entryPos = entryBase + i * 16;
            if (BigEndian.ReadUInt32(data, entryPos) != 7) continue;

            int dataOffset = (int)BigEndian.ReadUInt32(data, entryPos + 8);
            int absPos = entryPos + 8 + dataOffset;

            var span = data.AsSpan(absPos, 0x68);
            var bone = BoneData.Read(span);
            var output = new byte[0x68];
            BoneData.Write(output, in bone);

            Assert.Equal(span.ToArray(), output);
            return;
        }
    }

    [Fact]
    public void TextureData_RoundTrip_ByteIdentical()
    {
        var data = File.ReadAllBytes(TestFile.HDB);

        int ftStart = 32;
        int ftCount = (int)BigEndian.ReadUInt32(data, ftStart);
        int entryBase = ftStart + 8;

        for (int i = 0; i < ftCount; i++)
        {
            int entryPos = entryBase + i * 16;
            if (BigEndian.ReadUInt32(data, entryPos) != 10) continue;

            int dataOffset = (int)BigEndian.ReadUInt32(data, entryPos + 8);
            int absPos = entryPos + 8 + dataOffset;

            var span = data.AsSpan(absPos, 0x1C);
            var tex = TextureData.Read(span);
            var output = new byte[0x1C];
            TextureData.Write(output, in tex);

            Assert.Equal(span.ToArray(), output);
            return;
        }
    }

    [Fact]
    public void VAHeaderData_RoundTrip_Synthetic()
    {
        var data = new byte[0x10];
        BigEndian.WriteUInt32(data, 0x00, 1024);
        BigEndian.WriteUInt32(data, 0x04, 0x1234);
        BigEndian.WriteUInt32(data, 0x08, 64);
        BigEndian.WriteUInt32(data, 0x0C, 0);

        var header = VAHeaderData.Read(data);
        Assert.Equal(1024u, header.ByteSize);
        Assert.Equal(0x1234u, header.FormatType);
        Assert.Equal(64u, header.VertexCount);
        Assert.Equal(0u, header.Reserved0C);

        var output = new byte[0x10];
        VAHeaderData.Write(output, in header);
        Assert.Equal(data, output);
    }

    [Fact]
    public void FileHeader_RoundTrip_ByteIdentical()
    {
        var data = File.ReadAllBytes(TestFile.HDB);

        var span = data.AsSpan(0, 0x20);
        var header = FileHeader.Read(span);
        var output = new byte[0x20];
        FileHeader.Write(output, in header);

        Assert.Equal(span.ToArray(), output);
        Assert.Equal(0x42444840u, header.Magic);
    }

    [Fact]
    public void IndexBlockHeader_RoundTrip_Synthetic()
    {
        var data = new byte[0x10];
        BigEndian.WriteUInt32(data, 0x00, 0x200);
        BigEndian.WriteUInt32(data, 0x04, 0x100);
        BigEndian.WriteUInt32(data, 0x08, 0);
        BigEndian.WriteUInt32(data, 0x0C, 0);

        var header = IndexBlockHeader.Read(data);
        Assert.Equal(0x200u, header.ByteSize);
        Assert.Equal(0x100u, header.ByteSizeDuplicate);

        var output = new byte[0x10];
        IndexBlockHeader.Write(output, in header);
        Assert.Equal(data, output);
    }

    [Fact]
    public void VASetupRecord_RoundTrip_Synthetic()
    {
        var data = new byte[0x0C];
        BigEndian.WriteUInt32(data, 0x00, 256);
        BigEndian.WriteUInt32(data, 0x04, 0x13F00000);
        BigEndian.WriteUInt32(data, 0x08, 0x400);

        var rec = VASetupRecord.Read(data);
        Assert.Equal(256u, rec.VertexCount);
        Assert.Equal(0x13F00000u, rec.FormatType);
        Assert.Equal(0x400u, rec.Offset);

        var output = new byte[0x0C];
        VASetupRecord.Write(output, in rec);
        Assert.Equal(data, output);
    }

    [Fact]
    public void ModelRecord_RoundTrip_Synthetic()
    {
        var data = new byte[0x24];
        BigEndian.WriteInt32(data, 0x00, -16);
        BigEndian.WriteInt32(data, 0x04, 1);
        BigEndian.WriteInt32(data, 0x08, 32);
        BigEndian.WriteInt32(data, 0x0C, 2);
        BigEndian.WriteInt32(data, 0x10, 64);
        for (int i = 0; i < 8; i++)
            BigEndian.WriteUInt16(data, 0x14 + i * 2, (ushort)(i * 10));

        var entry = ModelRecord.Read(data);
        Assert.Equal(-16, entry.RenderCommandPtr);
        Assert.Equal(64, entry.VASetupPtr);
        Assert.Equal(0x003C0046u, entry.SphereRadiusBits);

        var output = new byte[0x24];
        ModelRecord.Write(output, in entry);
        Assert.Equal(data, output);
    }
}
