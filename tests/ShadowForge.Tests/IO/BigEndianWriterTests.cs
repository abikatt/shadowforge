using ShadowForge.IO;

namespace ShadowForge.Tests.IO;

public sealed class BigEndianWriterTests
{
    [Fact]
    public void WriteUInt16_BigEndian()
    {
        using var ms = new MemoryStream();
        using var writer = new BigEndianWriter(ms, leaveOpen: true);
        writer.WriteUInt16(0x1234);
        writer.Flush();
        Assert.Equal(new byte[] { 0x12, 0x34 }, ms.ToArray());
    }

    [Fact]
    public void WriteUInt32_BigEndian()
    {
        using var ms = new MemoryStream();
        using var writer = new BigEndianWriter(ms, leaveOpen: true);
        writer.WriteUInt32(0xDEADBEEF);
        writer.Flush();
        Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, ms.ToArray());
    }

    [Fact]
    public void WriteFloat_BigEndian()
    {
        using var ms = new MemoryStream();
        using var writer = new BigEndianWriter(ms, leaveOpen: true);
        writer.WriteFloat(1.0f);
        writer.Flush();
        Assert.Equal(new byte[] { 0x3F, 0x80, 0x00, 0x00 }, ms.ToArray());
    }

    [Fact]
    public void WriteFixedBytes_PadsWithZeros()
    {
        using var ms = new MemoryStream();
        using var writer = new BigEndianWriter(ms, leaveOpen: true);
        writer.WriteFixedBytes(new byte[] { 0xAA, 0xBB }, 4);
        writer.Flush();
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0x00, 0x00 }, ms.ToArray());
    }

    [Fact]
    public void RoundTrip_UInt32()
    {
        using var ms = new MemoryStream();
        using (var writer = new BigEndianWriter(ms, leaveOpen: true))
        {
            writer.WriteUInt32(0x12345678);
            writer.Flush();
        }
        ms.Position = 0;
        using var reader = new BigEndianReader(ms);
        Assert.Equal(0x12345678u, reader.ReadUInt32());
    }
}
