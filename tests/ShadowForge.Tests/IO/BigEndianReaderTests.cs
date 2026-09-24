using ShadowForge.IO;

namespace ShadowForge.Tests.IO;

public sealed class BigEndianReaderTests
{
    [Fact]
    public void ReadUInt16_BigEndian()
    {
        var data = new byte[] { 0x12, 0x34 };
        using var reader = new BigEndianReader(new MemoryStream(data));
        Assert.Equal((ushort)0x1234, reader.ReadUInt16());
    }

    [Fact]
    public void ReadUInt32_BigEndian()
    {
        var data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        using var reader = new BigEndianReader(new MemoryStream(data));
        Assert.Equal(0xDEADBEEFu, reader.ReadUInt32());
    }

    [Fact]
    public void ReadFloat_BigEndian()
    {
        var data = new byte[] { 0x3F, 0x80, 0x00, 0x00 };
        using var reader = new BigEndianReader(new MemoryStream(data));
        Assert.Equal(1.0f, reader.ReadFloat());
    }

    [Fact]
    public void ReadAsciiString_StopsAtNul()
    {
        var data = new byte[] { 0x41, 0x42, 0x43, 0x00, 0x00, 0x00, 0x00, 0x00 };
        using var reader = new BigEndianReader(new MemoryStream(data));
        Assert.Equal("ABC", reader.ReadAsciiString(8));
    }

    [Fact]
    public void Seek_SetsPosition()
    {
        var data = new byte[] { 0x00, 0x00, 0x00, 0x00, 0xAB };
        using var reader = new BigEndianReader(new MemoryStream(data));
        reader.Seek(4);
        Assert.Equal(4, reader.Position);
        Assert.Equal((byte)0xAB, reader.ReadByte());
    }
}
