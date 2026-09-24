using ShadowForge.Formats.IPK;

namespace ShadowForge.Tests.IPK;

public sealed class LZSSDecoderTests
{
    [Fact]
    public void Decompress_LiteralBytes()
    {
        var input = new byte[] { 0xFF, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48 };
        var output = LZSSDecoder.Decompress(input, 8);
        Assert.Equal("ABCDEFGH"u8.ToArray(), output);
    }

    [Fact]
    public void Decompress_EmptyInput_ReturnsZeros()
    {
        var output = LZSSDecoder.Decompress(Array.Empty<byte>(), 4);
        Assert.Equal(4, output.Length);
        Assert.True(output.All(b => b == 0));
    }
}
