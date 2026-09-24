using ShadowForge.IO;

namespace ShadowForge.Tests.IO;

public sealed class AlignTests
{
    [Theory]
    [InlineData(0u, 0x80u, 0u)]
    [InlineData(1u, 0x80u, 0x80u)]
    [InlineData(0x80u, 0x80u, 0x80u)]
    [InlineData(0x81u, 0x80u, 0x100u)]
    [InlineData(7u, 0u, 7u)]
    public void Up_RoundsToMultiple(uint value, uint alignment, uint expected)
    {
        Assert.Equal(expected, Align.Up(value, alignment));
        Assert.Equal((long)expected, Align.Up((long)value, (long)alignment));
    }
}
