using ShadowForge.Formats.HDB;

namespace ShadowForge.Tests.HDB;

public sealed class RenderCommandStreamStageTests
{
    [Fact]
    public void Parse_StageWords_ConsumeTheirIndexByte()
    {
        byte[] bytes = { 0x01, 0x03, 0x61, 0x04, 0x62, 0x05, 0x05, 0xFF };
        var cmds = RenderCommandStream.Parse(bytes);

        Assert.Equal(4, cmds.Count);
        Assert.Equal(0x61, cmds[1].Opcode);
        Assert.Equal(new byte[] { 0x04 }, cmds[1].Data);
        Assert.Equal(0x62, cmds[2].Opcode);
        Assert.Equal(new byte[] { 0x05 }, cmds[2].Data);
    }

    [Fact]
    public void Write_RoundTripsStageWords()
    {
        byte[] bytes = { 0x61, 0x04, 0x62, 0x05, 0x60, 0x00 };
        Assert.Equal(bytes, RenderCommandStream.Write(RenderCommandStream.Parse(bytes)));
    }
}
