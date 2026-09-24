using ShadowForge.Formats.HDB;

namespace ShadowForge.Tests.HDB;

public sealed class EyeWordTests
{
    [Theory]
    [InlineData(0, 0x00, 0x6000)]
    [InlineData(0, 0x02, 0x6002)]
    [InlineData(1, 0x04, 0x6104)]
    [InlineData(2, 0x05, 0x6205)]
    [InlineData(1, 0x06, 0x6106)]
    public void MaterialWord_ComposesAndDecomposes(int stage, int tex, int word)
    {
        Assert.Equal((ushort)word, new MaterialWord(stage, tex).Encode());
        Assert.Equal(stage, MaterialWord.Decode((ushort)word).Stage);
        Assert.Equal(tex, MaterialWord.Decode((ushort)word).TextureIndex);
    }

    [Fact]
    public void EyeFormatWord_YieldsBodySkinnedDeclAndStride()
    {
        var (stride, decl) = VertexFormat.ComputeStrideAndDecl(VertexFormat.Eye);
        Assert.Equal(100, stride);
        Assert.Equal(0x13EFA000u, decl);
    }
}
