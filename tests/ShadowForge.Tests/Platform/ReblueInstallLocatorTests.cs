using ShadowForge.Platform;

namespace ShadowForge.Tests.Platform;

public sealed class ReblueInstallLocatorTests
{
    [Fact]
    public void GetInstallRoot_DoesNotThrow_AndReturnsNullOrExistingPath()
    {
        string? root = ReblueInstallLocator.GetInstallRoot();
        if (root != null)
            Assert.NotEqual("", root.Trim());
    }
}
