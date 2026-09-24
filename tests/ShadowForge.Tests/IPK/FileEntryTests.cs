using ShadowForge.Formats.IPK;

namespace ShadowForge.Tests.IPK;

public sealed class FileEntryTests
{
    [Fact]
    public void Entry_Properties()
    {
        var entry = new FileEntry
        {
            Name = "test.rpj",
            IsCompressed = true,
            CompressedSize = 100,
            Offset = 0x1000,
            OriginalSize = 200,
        };

        Assert.Equal("test.rpj", entry.Name);
        Assert.True(entry.IsCompressed);
        Assert.Equal(100u, entry.CompressedSize);
    }
}
