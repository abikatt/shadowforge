using ShadowForge.Formats.RPJ;
using ShadowForge.IO;
using ShadowForge.Scene;

namespace ShadowForge.Tests.RPJ;

public sealed class BinarySceneTests
{
    [Fact]
    public void Read_MinimalFile_ParsesHeader()
    {
        var data = new byte[0x2B8 + 0x70];

        data[0] = (byte)'0'; data[1] = (byte)'.'; data[2] = (byte)'2'; data[3] = (byte)'6';
        BigEndian.WriteUInt32(data, 0x274, 3);
        BigEndian.WriteUInt32(data, 0x2AC, 0x2B8);
        BigEndian.WriteUInt32(data, 0x2B8, 1);

        var rpj = BinaryScene.Read(data);

        Assert.Equal("0.26", rpj.Version);
        Assert.Equal(AreaType.Dungeon, rpj.AreaType);
        Assert.Single(rpj.Entries);
        Assert.Equal(1u, rpj.Entries[0].Id);
    }

    [Fact]
    public void Read_TwoEntryChain_FollowsLinkedList()
    {
        var data = new byte[0x2B8 + 0x70 * 2];

        BigEndian.WriteUInt32(data, 0x2AC, 0x2B8);
        BigEndian.WriteUInt32(data, 0x2A4, 0xE0);

        BigEndian.WriteUInt32(data, 0x2B8 + 0x00, 1);
        BigEndian.WriteUInt32(data, 0x2B8 + 0x6C, 0x70);

        BigEndian.WriteUInt32(data, 0x328 + 0x00, 2);
        BigEndian.WriteUInt32(data, 0x328 + 0x6C, 0);

        var rpj = BinaryScene.Read(data);

        Assert.Equal(2, rpj.Entries.Count);
        Assert.Equal(1u, rpj.Entries[0].Id);
        Assert.Equal(2u, rpj.Entries[1].Id);
    }

    [Fact]
    public void Read_EmptyEntryPool_ReturnsNoEntries()
    {
        var data = new byte[0x2B8];
        BigEndian.WriteUInt32(data, 0x2AC, 0);

        var rpj = BinaryScene.Read(data);

        Assert.Empty(rpj.Entries);
    }
}
