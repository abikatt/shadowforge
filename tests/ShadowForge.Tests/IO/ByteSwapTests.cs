using ShadowForge.IO;

namespace ShadowForge.Tests.IO;

public sealed class ByteSwapTests
{
    [Fact]
    public void Swap16_SwapsPairs_LeavesOddTail()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        ByteSwap.Swap16(data);
        Assert.Equal(new byte[] { 2, 1, 4, 3, 5 }, data);
    }
}
