using ShadowForge.Formats.HDB;
using ShadowForge.Formats.HDB.Raw;

namespace ShadowForge.Tests.HDB;

public sealed class RawEntryComputedFieldsTests
{
    [Fact]
    public void PaddingEntry_TypeIsZero_LengthMatchesPayload()
    {
        var e = new RawPaddingEntry { Payload = new byte[13] };
        Assert.Equal(0, e.ComputedDiskEntryType);
        Assert.Equal(13, e.ComputedPayloadLength);
    }

    [Fact]
    public void FaceCountEntry_TypeIs4_LengthIs8()
    {
        var e = new RawFaceCountEntry { Count = 42 };
        Assert.Equal(4, e.ComputedDiskEntryType);
        Assert.Equal(8, e.ComputedPayloadLength);
    }

    [Fact]
    public void TextureCountEntry_TypeIs11_LengthIs8()
    {
        var e = new RawTextureCountEntry { Count = 3 };
        Assert.Equal(11, e.ComputedDiskEntryType);
        Assert.Equal(8, e.ComputedPayloadLength);
    }

    [Fact]
    public void UnknownEntry12_TypeIs12_LengthMatchesPayload()
    {
        var e = new RawUnknownEntry12 { Payload = new byte[36] };
        Assert.Equal(12, e.ComputedDiskEntryType);
        Assert.Equal(36, e.ComputedPayloadLength);
    }

    [Fact]
    public void UnknownGenericEntry_TypePreservedFromDisk_LengthMatchesPayload()
    {
        var e = new RawUnknownGenericEntry
        {
            DiskEntryType = 42,
            Payload = new byte[7],
        };
        Assert.Equal(42, e.ComputedDiskEntryType);
        Assert.Equal(7, e.ComputedPayloadLength);
    }

    [Fact]
    public void TextureTableEntry_TypeIs10_LengthIs28TimesRecords()
    {
        var e = new RawTextureTableEntry();
        e.Records.Add(new RawTextureRecord { Name = "a" });
        e.Records.Add(new RawTextureRecord { Name = "b" });
        Assert.Equal(10, e.ComputedDiskEntryType);
        Assert.Equal(56, e.ComputedPayloadLength);
    }

    [Fact]
    public void VASetupEntry_TypeIs5_LengthIs4PlusRecordsTimes12()
    {
        var e = new RawVASetupEntry();
        e.Records.Add(new RawVASetupRecord());
        e.Records.Add(new RawVASetupRecord());
        e.Records.Add(new RawVASetupRecord());
        Assert.Equal(5, e.ComputedDiskEntryType);
        Assert.Equal(40, e.ComputedPayloadLength);
    }

    [Fact]
    public void RenderCommandEntry_TypeIs3_LengthMatchesSerialized()
    {
        var e = new RawRenderCommandEntry();
        e.Commands.Add(new RenderCommand { Opcode = 0x40, Data = new byte[3] });
        e.Commands.Add(new RenderCommand { Opcode = 0x00, Data = new byte[] { 0xFF } });
        Assert.Equal(3, e.ComputedDiskEntryType);
        Assert.Equal(1 + 3 + 1 + 1, e.ComputedPayloadLength);
    }

    [Fact]
    public void ModelEntry_TypeIs6_LengthIs36()
    {
        var e = new RawModelEntry();
        Assert.Equal(6, e.ComputedDiskEntryType);
        Assert.Equal(36, e.ComputedPayloadLength);
    }

    [Fact]
    public void BoneEntry_TypeIs7_LengthIs104PlusTrailing()
    {
        var e = new RawBoneEntry
        {
            Name = "root",
            TrailingBytes = new byte[5],
        };
        Assert.Equal(7, e.ComputedDiskEntryType);
        Assert.Equal(0x68 + 5, e.ComputedPayloadLength);
    }

    [Fact]
    public void BoneEntry_NoTrailing_LengthIs104()
    {
        var e = new RawBoneEntry { Name = "root" };
        Assert.Equal(0x68, e.ComputedPayloadLength);
    }

    [Fact]
    public void UnknownEntry9_TypeIs9_LengthIs24TimesRecords()
    {
        var e = new RawUnknownEntry9();
        e.Records.Add(new uint[6]);
        Assert.Equal(9, e.ComputedDiskEntryType);
        Assert.Equal(24, e.ComputedPayloadLength);
    }

    [Fact]
    public void RenderCommandStream_GetSerializedLength_MatchesWriteLength()
    {
        var cmds = new List<RenderCommand>
        {
            new() { Opcode = 0x40, Data = new byte[3] },
            new() { Opcode = 0x60, Data = new byte[1] },
            new() { Opcode = 0x00, Data = new byte[] { 0xFF } },
        };
        var bytes = RenderCommandStream.Write(cmds);
        var computed = RenderCommandStream.GetSerializedLength(cmds);
        Assert.Equal(bytes.Length, computed);
    }
}
